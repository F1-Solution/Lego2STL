# The run is the document: a rail, a manifest, and a window that stops lying

**Date:** 2026-08-26
**Status:** approved design, not yet implemented
**Chosen from:** `docs/superpowers/specs/2026-08-25-gui-proposals.md` Part 4 — The Rail + Cool
neutral + The Run Is The Document.

## The problem

The window treats a run as an event that happens once and evaporates. It keeps nothing, so the
question *did I already print this set?* has no answer anywhere in the program. It also reports
a run that could not be verified as **"Done"**, with a full progress bar, while the command line
returns exit code 2 for the same run — the largest gap between the two front ends.

Separately, `MainViewModel` is 407 lines and every view binds straight into it. Adding a run
history, a merged setup screen and an option filter to that shape would land it well past the
800-line ceiling in the project's coding standards.

This design makes the **run folder** the thing the window is about, replaces the four fake tabs
with a four-item rail, and folds in four defects that are not matters of taste.

## Measured facts this design rests on

Checked in the source before designing, because three plausible assumptions turned out to be
wrong.

| Checked | Result |
|---|---|
| `RunReport.WriteAsync` call sites | **One**, at `PipelineRunner.cs:320`, inside `BuildShapesAsync`. The `Unverified`-from-unread exit (line 96) and the `--csv-only` exit (line 109) both return **without writing `report.txt`**. A manifest cannot simply sit "beside" it. |
| An STL reader in Core | **None.** `StlWriter` writes binary and text and can count triangles in a binary file; nothing reads a mesh back. Both shape warnings need an `IndexedMesh`, which exists only during a build. |
| `RunLayout.For` folder reuse | **Real and documented**: "When the parts list of a previous run is the input, the existing run folder is reused rather than nesting another inside it." `RunLayout.cs:64`. |
| `UniformGridLayout` in Avalonia 12.1.1 | **Absent from all 23 Avalonia assemblies this build produces.** `VirtualizingStackPanel` exists but stacks rather than wraps. A virtualised card grid would be a new package. |
| `ThemeVariantScope`, `SplitView`, `GridSplitter`, `MaxLines` | All present in `Avalonia.Controls.dll` 12.1.1. |
| `RunSettings.Problems()` | Returns **hardcoded English**. Validates `PlateSize` unconditionally — `WantsPlates` exists at line 137 and is not consulted. |
| `--quiet` | Registered by the CLI (`PipelineOptions.cs:133`, added to `command.Options`) and absent from `RunOptionsViewModel`. The guarding test walks a **hardcoded array of 21 names** that omits it, so it passes green. |
| `MainViewModel.StartAsync` | Maps `Complete` **and** `Unverified` to `UiDone`, sets `Progress = 1` for both, and switches to Catalogue for both. `RunOutcome.Unread`, `.Failed` and `.Notes` are bound by nothing in the GUI. |
| `OcrEngines.IsAvailable` | Referenced nowhere outside its own file. `TextKey.ErrOcrUnavailable` exists in both languages and is used nowhere. |
| `RunLayout.ReviewDirectory` / `OverridesPath` | Defined, and **written by nothing**. Out of scope here; recorded so it is not mistaken for new. |

Two consequences follow directly and are not negotiable within this design:

- **The manifest needs its own lifecycle**, not `report.txt`'s. It is written at five points.
- **The two shape warnings must be recorded when computed**, because they cannot be recovered
  from disk without a reader that does not exist — and even with one, an STL is a triangle soup
  with no shared vertices, so a re-welded open-edge count depends on the welding tolerance and
  could legitimately disagree with what the original run reported.

## The central idea: one projection, two sources

A run's presentable state is computed by exactly one function:

```
RunDocument.From(RunManifest, RunLayout)
```

The manifest reaches it two ways, and only two:

- **live** — `RunOutcome` → `RunManifest.From(outcome)` → written to disk *and* projected
- **reopened** — `run.json` → `RunManifest` → projected

A live run and a reopened run are therefore identical **by construction**, not by discipline.
There is no second catalogue builder to forget to update, and no "the reopened one is missing
the counts" bug to find later. One test defends this claim directly; see *Testing*.

`RunDocument` carries a **live facet** — `Progress`, `StageText`, `Log`, `Cancel` — which is
inert on a reopened run, whose log is read from `run.log` instead.

Because the manifest is written before the pipeline starts, `From` must also handle a manifest
whose status is `running` and which has no parts, no counts and no outcome yet. That is the state
a live run is projected from at second one, and it is the same state a reopened row shows for a
run that was killed mid-flight — so it is a first-class case, not an edge one.

## What comes out

### Core — three new files in `Core/Run/`, beside the existing `RunLayout`

| File | Job |
|---|---|
| `RunManifest.cs` | The record, its JSON shape, `From(RunOutcome)`, read and write |
| `RunFolder.cs` | Reads a folder back — manifest, parts list, which files are actually present |
| `RunIndex.cs` | The history. Called by `ConsoleRun` and by the window, **never** by `PipelineRunner` |

`AppDataDirectory` — the `LEGO2STL_SETTINGS_DIR`-or-`ApplicationData/Lego2STL` rule — moves into
Core so `RunIndex` and `UserSettings` share one definition rather than drifting apart. That is
the only tidying this design takes on; it is taken on because both new and existing code need
the same answer.

`PipelineRunner` gains manifest writes at its exits and nothing else. It never touches the user
profile, so embedding it or running it in CI still writes nothing outside the folder it was
given.

### The window — a shell and four screens

```
MainViewModel (shell)        rail selection, language, theme
 ├─ RunsViewModel            the list, newest first
 ├─ SetupViewModel           input + the option list
 │   ├─ RunOptionsViewModel  unchanged — still the parity surface
 │   └─ OptionRowsViewModel  search / changed-only / reset over the run-shaping options
 ├─ RunDocument?             the open run — live or from disk
 │   └─ CataloguePartViewModel   unchanged
 └─ SettingsViewModel        --api-key, --log, --quiet, language, forget history
```

Views split to match and stay small: `SetupView` hosts today's `InputView` plus a new
`OptionListView`; `RunDocumentView` hosts a progress panel, the log, and today's
`CatalogueView`. Nothing new is above roughly 250 lines.

`Screen`, `ShowCommand`, `OnInput`, `OnOptions`, `OnRun` and `OnCatalogue` are deleted. Rail
selection is the state.

## The manifest

`run.json`, in the run folder, schema `"version": 1`.

**Written five times, not once:**

1. **Before the pipeline starts** — status `running`, so the row exists and the settings survive
   a crash of the window.
2. `Unverified` from unread entries — `PipelineRunner.cs:96`.
3. `Complete` from `--csv-only` — `PipelineRunner.cs:109`.
4. The shapes exit — `PipelineRunner.cs:306`.
5. The `Failure` wrapper, and **cancellation**, which needs a `try/finally` in `RunAsync`
   because it currently leaves as an exception and would otherwise strand a manifest reading
   `running` forever.

**Contents:** status and timestamps; the `RunSettings`; the equivalent command line; counts;
`unread[]`, `failed[{part, reason}]` and `notes[]`; the last stage reached with its
completed/total, which is what lets a row say *"stopped while building shapes, 38 of 91"*; and
per part the colour, quantity, size, `isClosed`, `openEdgeCount` and `thinnestSpanMm`.

**The API key is masked as `<your key>`**, exactly the substitution `ToCommandLine` already
makes. One rule, applied in one helper both call.

**Reading is defensive**, following `UserSettings`' existing precedent that an unreadable file is
treated as no file:

| State | What the window does |
|---|---|
| Missing | The pre-manifest banner and **Run it again** — see below |
| `version` newer than known | Say it was made by a newer build; show what parses |
| Corrupt | Treated as missing |

### Folders written before manifests existed

Their two shape warnings are unrecoverable. The window says so and offers **Run it again**,
which opens Setup pre-filled as a parts-list run pointing at `<folder>/<name>.csv`. Because
`RunLayout.For` reuses a folder whose name matches its parts list, that rerun **fills the
manifest in place** rather than making a duplicate. No new reader, no fidelity risk, and it
rides on behaviour that already exists.

### One behaviour change, stated plainly

The window fills `--log` with `<planned folder>/run.log` when the user has not set one,
recomputed as the input changes and **visible in the shown command line**, so the log survives a
crash of the window and a terminal user running the shown line writes the same file. The user
can clear or override it. The CLI's own default is untouched.

## The index

**The index is an ordered set of folder paths and nothing else.** Every field a row displays —
status, counts, when, source — is read from that folder's own `run.json`. The rule *the index is
a convenience, the folder is the truth* becomes literally true, because there is no cached
summary that could disagree with a manifest.

`RunIndex.Record(layout)` is called **at the start** of a run, not at the end, so a run that
crashes or is killed still has a row. Two call sites, deliberately: `ConsoleRun` and the
window's runner. `PipelineRunner` is not one of them.

- **Atomicity** — writes go through a temp file and an atomic replace, because the terminal and
  the window can run at once. Because the index holds only paths, last-writer-wins loses nothing.
- **Reading is off the UI thread** — opening Runs reads N small manifests, which fill in as they
  arrive, exactly the pattern `MainViewModel.LoadPicturesAsync` already uses for thumbnails.
- **Rows whose folder is gone** are greyed and offer only *forget*. Settings offers *forget
  everything*, because the index records paths inside the user's home directory and removing it
  has to be one click rather than a file hunt.

## The window

### The rail

```
Runs        4     ← count
Setup       3     ← non-default options, or a problem badge
42100-1   61%     ← the open run: name + progress, or its status
Settings
[ New run ]
```

**A conflict between the two chosen proposals, resolved and recorded.** The Rail put `Start` at
the rail's foot, reachable from everywhere. The Run Is The Document puts `Start` on Setup and
nowhere else — and that single home is precisely what makes the wrong-screen problem structurally
impossible. They cannot both hold. This design resolves it in the flow's favour: **the rail foot
carries *New run*; `Start` lives on Setup.** `Start` is therefore unreachable from an empty list,
which was the original complaint.

### The command line keeps both homes

The footer shows Setup's live line while setting up. A run's page shows that run's **stored**
line in its header, so a run from three weeks ago can be reproduced in a terminal without
reconstructing what was ticked. That extends the parity promise past the moment of pressing
Start, which is the only place it currently reaches.

### The option list

**Where each option lives, stated exactly, because the parity test depends on it.** The CLI
registers 22 options into `command.Options`, plus `--lang` and the conditional `--color-scheme`.
They divide in two:

| Screen | Options |
|---|---|
| **Setup** | The input block — `--include-spares` and `--color-scheme` — plus the **18** run-shaping options: `--csv-only`, `--no-plates`, `--output-dir`, `--delimiter`, `--ascii`, `--keep-origin`, `--scale`, `--clearance`, `--repair`, `--no-seam-repair`, `--weld-tolerance`, `--ldraw-dir`, `--ldraw-cache`, `--offline`, `--no-unofficial`, `--printer`, `--plate-size`, `--plate-spacing` |
| **Settings** | The four that are about this machine rather than this run: `--api-key`, `--log`, `--quiet` — and `--lang`, which is outside the 22 |

That accounts for all 22 exactly: 18 run-shaping + `--include-spares` + `--api-key`, `--log`,
`--quiet`. `--lang` and the conditional `--color-scheme` sit outside that set.

Nothing appears on both. That is also what removes today's duplicated language menu: `--lang`
has exactly one home, plus the header menu, which stays because a language switch has to be
findable when you cannot read the rail.

The Setup list carries three tools: **search** across flag, label and help text;
**changed-only**, driven by what `ToCommandLine` already computes so it costs nothing; and a
per-row **reset**. Changed-only is the default from the second run onward, and its toggle
**carries the count of hidden rows**, so an option set three runs ago cannot sit invisible
without the window saying how many are hidden.

Filtering hides rows with `IsVisible`, never by re-materialising the list, so filtered rows stay
in the logical tree and the parity test can still read them. The test additionally turns the
filter off, so it does not depend on that subtlety holding.

### Cool neutral

Applied as `ResourceDictionary.ThemeDictionaries` keyed Light/Dark in `App.axaml`, not as
scattered literals, together with overrides of `SystemAccentColor` and
`SystemAccentColorLight1/2/3` / `Dark1/2/3` — that one move recolours every Fluent checkbox,
radio, spinner and focus ring at once, which is how the window stops borrowing the OS accent.

| Token | Light | Dark |
|---|---|---|
| Window background | `#F4F6F8` | `#0F1216` |
| Surface / card | `#FFFFFF` | `#171B21` |
| Card border | `#DDE2E8` | `#2A313A` |
| Primary text | `#14181D` | `#E8ECF1` |
| Secondary text | `#55606C` | `#9BA6B3` |
| Accent | `#0B6BCB` | `#6BA6F5` |
| Accent hover | `#0A5AAA` | `#8CBCFF` |
| Text on accent | `#FFFFFF` | `#06182E` |
| Success | `#16794C` | `#58C99A` |
| Warning text | `#8A5A00` | `#E2B04A` |
| Warning band / border | `#FDF0D2` / `#E3C77E` | `#2E2510` / `#5A4A1F` |
| Danger | `#C0271B` | `#F2867C` |
| Log background / foreground | `#1B1F24` / `#DDE3EA` | `#0B0E12` / `#D6DEE7` |

Primary text on ground 16.45:1 light and 15.83:1 dark; text on accent 5.28:1 and 7.13:1. Every
pair clears 4.5:1, floor 4.87. Recomputed rather than taken on trust.

- `RequestedThemeVariant="Default"` stays, so the OS decides light or dark.
- The log is wrapped in a `ThemeVariantScope` forced to Dark, so it is a terminal in both themes
  with a scrollbar that matches it rather than the window.
- Today's translucent `#33FFAA00` warning band becomes opaque per theme — an alpha over an
  unknown surface is why the current band goes muddy in dark mode.
- **No uppercase anywhere**, so Italian never has to render `QUANTITÀ` in accented capitals,
  which are unreliable across the three platforms' font fallback chains.
- The rail pane is 200 px open, sized for `Esecuzione` plus a status of `Non riuscito`; the
  selected row is a 3 px accent bar plus a surface fill, not a full accent tint.

### The `ViewLocator` cache

**Task one, before the `SplitView` lands.** `ViewLocator.Build` currently calls
`Activator.CreateInstance` on every request. It gains a cache keyed by view-model **instance**,
so each open run keeps its own view and its own scroll position. Without it, replacing the
always-alive `Panel` costs both the log's scroll position and the option-parity test, whose
`GetLogicalDescendants` finds nothing in an unrealised view.

## The four defects, folded in

**1. `Unverified` stops reading "Done".** A run has **four** statuses, each with its own word and
its own row colour — *Complete*, *Needs a decision*, *Failed*, *Stopped*. On a needs-a-decision run
the window stays on the run's page, the bar shows where it actually stopped rather than 100%, and
an amber card names the cause from data that exists today and is bound by nothing: `Unread`
(which labels could not be read) or `Failed` (which parts produced nothing, each with its
reason). Two exits: **Open the parts list**, and **Continue from the parts list**, which lands in
the same folder via `RunLayout.For`. The window and the CLI's exit code 2 finally agree.

**2. `Problems()` gets keys and a gate.** Its hardcoded English strings become `TextKey`s in both
languages; `RunSettings` already carries `Language`, so no signature changes. It gains
`Kind == Document && !OcrEngines.IsAvailable`, which means **the command line inherits the early
refusal too** rather than dying at `RunStage.ReadingDocument`. The `PlateSize` check gains its
missing `WantsPlates` guard, so a `--csv-only` run stops being blocked by a bed size no plate
would use.

**3. The folder button cannot lie, by construction.** The manifest is written before the run
starts, so a `RunDocument` — and its folder — exists from second one, through failure and
cancellation alike. `Log.Clear()` disappears: a new run is a *new document*, so the previous
run's log survives on its own page and in `run.log`.

**4. `--quiet`, and the test that could not see it.** `Quiet` joins `RunOptionsViewModel` and
appears in Settings. The parity test stops walking a hardcoded array and instead enumerates
`command.Options` from the real CLI registration, so it can never again pass green while an
option is missing from the window.

**Optional, droppable:** the *Find the catalogue pages* button shows its own equivalent
`extract --list-pages` line, closing the last parity leak.

## Error handling

The governing rule already exists in this codebase, in `Desktop.Open`: a convenience failing is
*not worth interrupting anyone over*.

| Situation | Behaviour |
|---|---|
| Manifest will not write | Logged; never interrupts the run. The folder degrades to the pre-manifest banner, a state already handled. |
| Manifest will not read | Missing → banner + *Run it again*; newer version → say so, show what parses; corrupt → treated as missing. |
| Index will not write | Swallowed, per `UserSettings.Save`. Losing a history row must never cost a run. |
| Two runs at once | Temp file + atomic replace. Only paths are stored, so last-writer-wins loses nothing. |
| Cancellation | `try/finally` in `RunAsync` writes a `stopped` manifest with the last stage reached. |
| Folder gone or drive disconnected | Row greyed, only *forget*. No dialog. |
| Adopt-a-folder with neither manifest nor parts list | Refused, with a reason. |

## Testing

TDD throughout. The highest-value test in the set is a single statement of intent:

> **A live outcome and the same run re-read from disk project to an equal `RunDocument`.**

That assertion defends the architecture's central claim. If it ever fails, live and reopened have
drifted and everything else is suspect.

**Unit — `Lego2STL.Tests`:** manifest round-trip and API-key masking; each of the five write
points producing the right status; defensive reads for missing, newer and corrupt;
`RunIndex` recording at start, de-duplicating paths, forgetting, and honouring
`LEGO2STL_SETTINGS_DIR`; `RunFolder.Read` against a fixture folder including the no-manifest
case; `Problems()` in both languages, with the OCR gate and the `--csv-only` fix.

**Interface — `Lego2STL.UiTests`:** all 12 existing tests need re-pointing.
`Every_option_the_command_line_takes_is_named_on_the_options_screen` is rewritten to enumerate
`command.Options` from the real CLI registration and to **union the text of Setup and Settings**,
because the options now divide across the two. Note this makes the test strictly stronger than
today's: the hardcoded array omits `--include-spares` and `--color-scheme` as well as `--quiet`.
It runs with changed-only off. `All_four_screens_draw` and
`A_picture_of_each_screen_is_written` follow the new rail. Two new: a reopened run and a live run
render the same catalogue; and switching rail items preserves the log's scroll position, which is
what stops the `ViewLocator` cache from silently regressing later.

Coverage target 80%, per the project's standards.

## Decisions taken, and why

| Decision | Why, and what was rejected |
|---|---|
| The index holds paths only | Rejected caching summaries in it: a cache can disagree with the manifest, and reading ~50 small files off the UI thread is trivial. |
| `RunIndex` is called by the two front ends, not by `PipelineRunner` | Rejected `PipelineRunner` writing it: running a pipeline would then write outside the folder it was given, polluting CI and any embedding caller, and two runs would race. |
| Old folders offer *Run it again* | Rejected writing an STL reader: it does not exist, and a re-welded open-edge count depends on a tolerance, so it could disagree with what the original run reported. |
| One `RunDocument`, two manifest sources | Rejected separate live and reopened view models: two presentations of one concept drift, and every future field would be added twice. |
| `Start` lives on Setup, rail foot carries *New run* | The two chosen proposals disagreed. Resolved for the flow, because a single home for `Start` is what makes the wrong-screen problem impossible. |
| `AppDataDirectory` moves to Core | Both `RunIndex` and `UserSettings` need the same rule; two copies would drift. |

## What this deliberately does not do

- **No virtualised card grid.** `UniformGridLayout` is not in this project's Avalonia and would
  be a new package. The catalogue keeps `ItemsControl` + `WrapPanel`. If a set ever makes that
  hurt, it is a separate, measured decision.
- **No review screen.** `RunLayout.ReviewDirectory` and `OverridesPath` remain unwritten. They
  are the largest designed-but-unbuilt behaviour in the program and they stay out of scope here.
- **No in-app theme switch.** The OS decides, as today.
- **No application icon.** Still Avalonia's default; noted in the proposals document and not
  addressed here.
- **No cap on history size.** Until there is evidence one is needed, *forget* and *forget
  everything* are enough.

## Known risks

1. **Scope.** This is three separable pieces — the defects, the rail, the run document — in one
   spec and one plan, at the user's explicit direction after the alternative was put to them.
   The implementation plan must therefore keep the seams clean enough to be executed and
   reviewed in stages even though it ships as one body of work.
2. **Twelve UI tests move at once.** They are the safety net for a full layout rewrite, and they
   are being re-pointed in the same change that rewrites the layout. The `ViewLocator` cache and
   the `command.Options` rewrite land first, so the net is re-hung before the wall comes down.
3. **`run.json` becomes a file other tools may read.** It is versioned from the first release for
   that reason.
