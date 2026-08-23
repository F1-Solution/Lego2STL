# Lego2STL — Implementation Plan

Status: **awaiting approval**. Nothing has been built yet.
Date: 2026-08-23

---

## 1. What this is

A Windows tool that turns a LEGO parts catalogue into a CSV plus 3D-printable geometry.

Three input modes, one pipeline:

```
  PDF + page range ──┐
  CSV (previous run) ┼──> normalised parts list ──> CSV ──> LDraw ──> STL + 3MF plates
  Set number ────────┘        (part, colour, qty)
```

Shipped as **two executables over one core library**, plus an Avalonia GUI exposing every CLI capability.

---

## 2. Verified facts (measured, not assumed)

Everything below was checked on this machine on 2026-08-22/23.

### The PDF

| Fact | Evidence |
|---|---|
| 126 pages, not 107 | `pymupdf` page count |
| **Zero text layer** — every page is one full-page JPEG, 1684x1192, 144 DPI | `page.get_text()` returns 0 chars; `get_images()` returns exactly 1 `/DCTDecode` XObject per page |
| Parts catalogue = pages **2-5**; page 6 is the "Building Instructions" divider | visual inspection of rendered pages |
| Label layout: `<qty>x` on one line, `<part>, <colour>` beneath | visual |
| Colour codes are **BrickLink** | `11`=Black, `5`=Red, `7`=Blue, `2`=Tan, `8`=Brown, `9`=Light Gray, `85`=Dark Bluish Gray, `86`=Light Bluish Gray. Rebrickable's `11` is Light Turquoise, so the numbering is BrickLink's |
| Part numbers are **BrickLink** too, and can carry letter suffixes | `4265c` |
| Ground truth: **53 labels** = 22 / 5 / 17 / 9 | manual transcription of all four pages |
| Glyphs are 17-18 px tall, 7-13 px wide | connected-component measurement |
| Part-render pixel colours match **Rebrickable** RGB best | Black `#010713` vs Rebrickable `#05131D` / LDraw `#1B2A34` / BrickLink `#2E2E2E`; Red `#D30F01` vs `#C91A09` / `#B40000` |

### Extraction (empirically validated end to end)

| Finding | Measurement |
|---|---|
| Windows' built-in OCR on a **full page** is useless | returned `"' 17"`, `"34>7"`, `"40<90"` |
| Windows OCR on a **tight crop at native resolution** is excellent | `"5x"` + `"32524, 11"` — exactly right |
| **Upscaling hurts** | a 150-DPI crop upscaled 4x lost the colour code; the native 139x52 crop read both lines perfectly |
| A **white margin** around the crop removes spurious digits | `"32250, 111"` became `"32250, 11"`; two empty results became correct |
| Dilation-based label location: **dy in {16,18,20} x dx in {16,20} gives exactly 53 labels, no fragments** | a 6-cell stable plateau, so a robust choice not a lucky fit. `dx>=26` merges adjacent labels; `dy<=14` splits them |
| Whole-label OCR: **40/53 exact** | all 13 failures were the *same* failure — the engine silently dropped the quantity line while reading the part line perfectly |
| Per-row projection profile splits 49/53 labels into exactly 2 bands | 4 outliers still to handle |
| `1` vs `I`/`l` confusion is systematic and fully correctable | `Ix`, `lx`, `IOX`, `1 ox` — the quantity line is always `<digits>x` |

**Conclusion:** OCR must run on **one text row at a time**, cropped at native resolution, with a white margin, under a per-row constrained grammar. That is the design.

### LDraw

| Fact | Evidence |
|---|---|
| A `.dat` is never self-sufficient | `32250` pulls in 37 files recursively (`2-4ring2`, `npeghole`, `axlehol4`, ...) from `p/` and `parts/s/` |
| Server needs a User-Agent, and rate-limits | **403** without UA; **429** after ~60 rapid requests |
| `complete.zip` = 144.7 MB, 36,896 entries | HTTP HEAD; entry count from the research |
| Alias stubs must be followed | `4265c.dat` is `0 ~Moved to 32123` with a single type-1 reference to `32123.dat` |
| Parts are **not watertight** — but mostly repairably so | see table below |
| LDraw geometry is **nominal**, not real part size | an LDraw 2x4 brick is exactly 32.000x9.600x16.000 mm; a real one is 31.920x9.560x15.920. Printed as-is, zero clearance on studs and holes |

Manifoldness, measured with a throwaway converter:

| Part | Triangles | Open edges | T-junctions (exactly repairable) | True holes |
|---|---|---|---|---|
| `3705` axle | 176 | 0 | — | **watertight already** |
| `32523` beam 3 | 1132 | 36 | **36 (100%)** | **0** |
| `4265c` bush | 480 | 48 | **48 (100%)** | **0** |
| `32017` | 2004 | 68 | **68 (100%)** | **0** |
| `32250` panel | 2772 | 334 | 196 (58%) | 138 |
| `2780` pin | 1504 | 1448 | 648 (44%) | 800 |

No part had any edge shared by more than 2 faces, so there are no duplicate or coincident faces — the meshes are clean apart from boundary holes. **T-junction splitting is an exact repair** (it splits an edge at a vertex already lying on it, inventing nothing) and alone makes several parts fully watertight.

Also measured: naive round-to-N-decimals welding is **unstable** (2 decimals scored *worse* than 3 — a rounding-boundary artifact). Welding must use a spatial hash, not rounding.

### Rebrickable

| Fact | Evidence |
|---|---|
| API key works | `/lego/colors/` returns 275 colours; `/lego/sets/42100-1/parts/` returns 243 parts |
| `external_ids` gives the full cross-reference | Black to BrickLink `[11]`, LEGO `[26, 342]`, LDraw `[0, 256]`, BrickOwl, Peeron |
| ...but values are **arrays**, one-to-many | LDraw `[0, 256]` where 256 is `Rubber_Black` — needs a primary-id rule, not take-first |
| `external_ids.LDraw` is the correct part-to-LDraw mapping | `32523` gives `['32523']` |
| **BrickLink part numbers are not Rebrickable part numbers** | `/lego/parts/4265c/` returns *"No Part matches"*. `?bricklink_id=4265c` returns **two** parts, `32123a` and `32123b` |
| The local `DB Lego` dump has **no BrickLink column** | `parts.csv` is `part_num,name,part_cat_id,part_material`; `colors.csv` has no external ids |
| Colour-specific LDraw renders exist — ideal for the GUI catalogue | `cdn.rebrickable.com/media/parts/ldraw/11/32523.png` |

**Consequence:** validating OCR output against the local `parts.csv` alone would **wrongly reject `4265c`**. The validation set must be the union of the LDraw part list, Rebrickable part numbers, and BrickLink ids.

### Environment

| Fact | Evidence |
|---|---|
| .NET SDK 8.0.423, 10.0.110, 10.0.300 | `dotnet --list-sdks` |
| Avalonia **12.1.1** stable; `avalonia.mvvm` template already installed | NuGet flat-container; `dotnet new list` |
| `System.CommandLine` **2.0.11** is stable (the long beta is over) | NuGet |
| CommunityToolkit.Mvvm 8.4.2 | NuGet |
| Windows OCR available, `en-GB` engine, `MaxImageDimension` 10000 | ran it |
| **No** slicer, **no** OpenSCAD, **no** LDraw install, no Tesseract | filesystem checks |

---

## 3. Decisions locked during the interview

| # | Decision |
|---|---|
| 1 | **Extraction:** local OCR plus cross-validation. No cloud, no API key at runtime |
| 2 | **CSV schema:** `ID; Codice Lego; Codice BrickLink; Nome colore; Codice RGB; Quantita` |
| 3 | `--color-scheme {Lego\|Rebrickable\|BrickLink}` says how to read the PDF's numbers; the `Codice BrickLink` column **always** holds the true BrickLink id, translated when needed. Unmappable codes are reported, never guessed |
| 4 | **RGB source:** Rebrickable, for both the CSV column and the pixel cross-check (measured closest to the PDF) |
| 5 | **Rows:** one per (part, colour), duplicates summed, IDs 1..N in page reading order |
| 6 | **On uncertainty:** prompt interactively, auto-opening the crop; `--non-interactive` or no TTY writes the CSV, flags the report, exits non-zero and refuses the STL stage |
| 7 | **Layout:** one folder next to the input holds everything |
| 8 | **CSV delimiter:** semicolon by default (Italian Excel); delimiter sniffed on read; `--delimiter` to override |
| 9 | **LDraw source:** escalate local dir, then per-part fetch, then `complete.zip` |
| 10 | **Units:** mm, Z-up, resting on Z=0, XY-centred; `--keep-origin` to opt out |
| 11 | **STL:** binary, one file per **distinct part number**, named `<part>.stl`; `--ascii` available |
| 12 | **Page range:** 1-based inclusive; omitted means auto-detect and confirm; plus a `--list-pages` preview command |
| 13 | **Output scope:** STL plus coloured 3MF plates grouped by colour. **No gcode** |
| 14 | **Clearance:** faithful nominal geometry by default; `--clearance <mm>` applies a uniform inward offset; `--calibration` emits a small test set at several values |
| 15 | **Language:** `--lang {en\|it}` switches help, messages, prompts and the report. CSV headers stay Italian regardless, since they are the specified schema |
| 16 | **Tests:** snapshot tests with `--update-snapshots`, plus a unit layer |
| 17 | **MachineBlocks:** a **separate command**, never bundled (CC BY-NC-SA) |
| 18 | **Language/runtime:** C# / .NET, self-contained single-file executable |
| 19 | **Set mode:** `--set <num>` pulls the inventory from the Rebrickable API |
| 20 | **GUI:** Avalonia, full parity with the CLI |

---

## 4. Architecture

```
Lego2STL.sln
  Lego2STL.Core        net8.0-windows10.0.19041.0   all logic, no UI
  Lego2STL.Cli         net8.0-windows10.0.19041.0   console, System.CommandLine 2.0.11
  Lego2STL.Gui         net8.0-windows10.0.19041.0   Avalonia 12.1.1 + CommunityToolkit.Mvvm
  Lego2STL.Tests       net8.0-windows10.0.19041.0   xUnit: unit + snapshot
```

The Windows TFM is forced by the OCR engine. Everything OCR-related sits behind `IOcrEngine`, so a cross-platform engine can be added later without touching the pipeline.

### Dependencies

| Concern | Choice | Why |
|---|---|---|
| PDF image extraction | **PdfPig 0.1.16** | pure managed, **no native payload** — keeps the single-file exe clean. Extracts all 126 embedded JPEGs losslessly in about 84 ms |
| PDF rasterising (fallback) | **PDFtoImage 5.4.0** | for pages that are not a single image; shares `SKBitmap` with PdfPig so there is one pixel-sampling code path |
| OCR | **`Windows.Media.Ocr`** | no native payload, no model download, no install; **measured excellent** on tight crops |
| CLI | **System.CommandLine 2.0.11** | stable |
| GUI | **Avalonia 12.1.1** plus CommunityToolkit.Mvvm 8.4.2 | requested |
| STL and 3MF writing | **hand-rolled** | no library needed; 3MF is ZIP plus XML, verified to pass lib3mf 2.5.0 strict validation with per-object colour. No maintained .NET 3MF library exists outside commercial Aspose.3D |
| Vertex weld, T-junction split | **hand-rolled** | nothing in .NET implements T-junction splitting; a quantised spatial hash is the right weld |
| Boundary loops and hole fill | **geometry3Sharp** (Boost licence), vendored | genuinely non-trivial; only needed for opt-in `--repair` |
| Maths | `System.Numerics` | sufficient — see the trap below |

### The one trap that must not be missed

`System.Numerics.Matrix4x4` is **row-vector** (`vM`, translation in `M41..M43`). LDraw type-1 lines are **column-vector** (`p' = M*p + t`). The 3x3 **must be transposed on load**:

```csharp
new Matrix4x4(a, d, g, 0,      // a d g — NOT a b c
              b, e, h, 0,
              c, f, i, 0,
              x, y, z, 1);
```

A naive load is **silently wrong** — measured: correct `<13,21,26>` versus naive `<12,14,31>`. And **the determinant check does not catch it**, because transposing preserves the determinant (both gave -2). This gets its own unit test with a deliberately asymmetric, non-orthogonal matrix.

---

## 5. Pipeline

1. **Resolve input** — PDF, CSV or set number, by extension or flag.
2. **Pages** — parse `2-5,8,11-13`; dedupe; out-of-range is an error naming the real page count. If omitted, classify pages and confirm.
3. **Locate labels** — extract the embedded JPEG; connected components; keep glyph-sized ones (6-22 px tall, 1-16 wide, at least 6 px); fill their boxes; dilate (dy=18, dx=18); blobs with 7 or more glyphs are labels.
4. **Split rows** — horizontal projection profile within each label; handle the 4 known outliers with more than 2 bands.
5. **OCR per row** — native-resolution crop plus an 18 px white margin; per-row constrained grammar (`^(\d+)x$` for quantity with `I l |` mapped to `1` and `O` to `0`; `^([0-9a-z]+),\s*(\d+)$` for the part line).
6. **Validate** — part against LDraw's part list, Rebrickable part numbers and BrickLink ids, with confusable-digit-weighted edit-distance repair; colour code to canonical colour; cross-check against sampled render pixels, abstaining on near-neighbour greys where the difference is smaller than shading noise.
7. **Review** — prompt on anything unresolved, auto-opening the crop; remember answers in an overrides file.
8. **Write CSV** — 6 columns, semicolon, UTF-8 BOM, IDs in page order.
9. **Acquire LDraw** — escalate; follow `~Moved to` aliases and report every redirect.
10. **Convert** — recursive resolution with the transposed matrix stack, BFC winding with `INVERTNEXT` and determinant-sign XOR, quads to triangles, spatial weld, drop degenerates, exact T-junction split.
11. **Optional** — `--clearance` uniform inward offset, refusing on non-watertight parts and naming them; `--repair` boundary-loop fill.
12. **Emit** — binary STL per distinct part; 3MF plates grouped by colour with per-object RGB, shelf-packed (LEGO heights cluster, so shelf packing lands within a few percent of MaxRects at a quarter of the code).
13. **Report** — per part: triangles, open edges, watertight yes/no, backend, substitutions, redirects, sub-nozzle-feature warnings.

---

## 6. CLI surface

```
lego2stl <input> [<pages>] [options]
lego2stl --set 42100-1 [options]
lego2stl --list-pages <pdf>
lego2stl calibration [options]
lego2stl bricks <spec> [options]        # MachineBlocks, separate command
lego2stl --refresh-colors [--api-key K]

Input        --set <num>  --include-spares  --color-scheme Lego|Rebrickable|BrickLink
Stages       --csv-only  --stl-only  --no-3mf
Output       --output-dir <dir>  --delimiter <c>  --ascii  --overwrite
Geometry     --keep-origin  --scale <pct>  --clearance <mm>  --repair
             --hi-res-primitives  --weld-tolerance <mm>
LDraw        --ldraw-dir <dir>  --offline  --ldraw-cache <dir>  --unofficial
Plates       --printer A1|A1mini|P1S|P1P|X1C|H2D  --plate-spacing <mm>  --no-plates
Behaviour    --non-interactive  --lang en|it  --verbose  --quiet  --log <file>
             --api-key <k>  --update-snapshots
```

Exit codes: `0` clean, `2` completed with unresolved rows, `1` hard failure.

---

## 7. Avalonia GUI

Four screens over the same `Lego2STL.Core`, driving the identical pipeline.

1. **Input** — pick PDF, CSV or set number; page range with a `--list-pages`-style page classification preview.
2. **Options** — every CLI flag as a control, grouped; shows the equivalent command line, so the GUI teaches the CLI.
3. **Run** — a determinate **progress bar** over pipeline stages, a **collapsible log** pane, and live **part thumbnails** appearing as each part resolves, pulled from `cdn.rebrickable.com/media/parts/ldraw/<colour>/<part>.png` and cached on disk. Interactive review appears here as an inline card with the crop image and candidate buttons, replacing the console prompt.
4. **Catalogue** — a navigable thumbnail grid of the extracted parts: image, part number, colour swatch in the real RGB, quantity, and per-item links to open the `.stl`, the `.3mf` plate, or the containing folder. Filter by colour, sort by part or quantity, and a warning badge on parts with sub-nozzle features or non-watertight geometry.

Packaging: separate `lego2stl.exe` (console) and `Lego2STL.Gui.exe` (WinExe), so neither inherits the other's console behaviour — a console app flashes a window, and a WinExe cannot write to stdout.

Self-contained publish:

```
dotnet publish -c Release -r win-x64 --self-contained \
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

Expect roughly 70 MB for the CLI and 90 MB for the GUI. **No trimming on the GUI** — Avalonia's XAML loading is reflection-based and trimming breaks it.

---

## 8. Honest limitations (to be stated in the README, not buried)

1. **Printed parts will not clutch like real LEGO without calibration.** FDM repeatability is plus or minus 0.1-0.2 mm; LEGO's clutch interference lives inside about 0.01 mm — a 4x to 20x gap.
2. **This set is the hard case.** It is almost all Technic: axle cross-arms (about 1.8 mm), pin walls and thin bushes (sub-mm) are at or below a 0.4 mm nozzle's floor. Beams are fine; pins, axles and bushes are marginal to failing at 1:1.
3. **LDraw geometry is nominal**, so it has zero designed-in clearance. `--clearance` is the correction; `--calibration` is how you find the right value.
4. **Most parts are not watertight.** Slicers auto-repair silently; the report tells you which parts needed it.
5. **No gcode.** Bambu Studio's `--export-gcode` is dead code, and all three slicers need a GUI-configured install to produce valid profiles. Guessed defaults are actively dangerous (`bed_temperature = 0`, Cool Plate, supports off).
6. **Image-to-3D was evaluated and rejected** — no metric scale, invents hidden interiors, 10x to 30x off the fit budget, and Hunyuan3D's licence excludes the EU.
7. **MachineBlocks is CC BY-NC-SA** — non-commercial and share-alike. Not bundled; used only via your own OpenSCAD and your own clone.

---

## 9. Build order

| Phase | Deliverable | Verification |
|---|---|---|
| 0 | Solution skeleton, `git init`, `.gitignore`, `PROGRESS.md` | builds |
| 1 | Colour cross-reference generated from the Rebrickable API and vendored | 275 colours, BrickLink/LEGO/LDraw/RGB all present |
| 2 | Page and range parsing, `--list-pages` | classifies pages 2-5 as parts lists, 6 as divider |
| 3 | Label locator plus row splitter | **finds exactly 53 labels** on pages 2-5 |
| 4 | OCR, grammar, validation, review | **53/53 rows correct** against the transcribed ground truth |
| 5 | CSV read/write round-trip | write, read, identical rows |
| 6 | LDraw acquisition, alias following | all 44 parts resolve; `4265c` reported as pointing to `32123` |
| 7 | Parser plus transform stack | **transpose unit test**; differential check against `davidmargol/lego2stl` on bounding box and triangle count |
| 8 | Weld, degenerates, T-junction split | `32523`, `4265c`, `32017` reach **0 open edges** |
| 9 | Binary STL writer | 44 files; `3705` measures 32.0 x 4.8 x 4.8 mm with min Z = 0 |
| 10 | 3MF plates by colour | opens in a viewer; per-object colours correct |
| 11 | `--clearance`, `--calibration` | offset measurable on a caliper-checkable test part |
| 12 | Snapshot test suite | golden CSV plus 44 STL, byte-stable |
| 13 | Avalonia GUI, four screens | every CLI capability reachable |
| 14 | MachineBlocks `bricks` command | generates a liftarm via a local OpenSCAD |
| 15 | Self-contained publish, README | runs on a machine with no .NET installed |

Phase 4's gate is the real one: **53/53, or the extraction is not done.**

Per the PROGRESS.md protocol, one line is appended per completed phase, and it is read before each phase starts so an interrupted run resumes rather than restarts.

---

## 10. Open items

1. **Rotate the Rebrickable API key** — it was pasted in plaintext. It will not be committed; runtime reads `REBRICKABLE_API_KEY` or `%APPDATA%\Lego2STL\config.json`.
2. **Which printer** should be the default for plate sizing?
3. **`--lang` default** — English with `--lang it` available, or Italian by default on an Italian OS?
