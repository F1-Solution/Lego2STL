# Calibration, And Remembering Its Answer — Design

**Date:** 2026-09-01
**Status:** approved 2026-09-01. Ready for an implementation plan.
**Covers:** sub-project C of `2026-08-31-print-quality-and-mobile-roadmap.md`.
**Depends on:** A+B, completed 2026-08-31. C reuses the preset and the sheet that a build plate
now carries, and the run record that says what was decided.

---

## What is already right, and must stay right

`lego2stl calibration` does the hard half well and this design changes none of it. It writes an
axle and the bush that runs on it — *"a fit is a property of two parts and not of one"* — at
clearances from 0.00 to 0.25 mm in steps of 0.05, with a note beside them, because *"a folder of
near-identical shapes is useless without one, and by the time they are printed the command line
that made them is long gone"*. And it refuses to offer a default clearance, on the grounds that the
figure being looked for is smaller than the difference between two machines of the same model.

That refusal is the command's founding argument. **It survives this design intact.** Nothing here
invents a clearance; everything here is about making the one you measured easier to obtain and
impossible to lose.

Three things are missing.

1. **The output is loose STLs** — one file per part per step, to be arranged in a slicer by hand,
   by a tool whose every other output is a packed plate.
2. **The set tests one kind of fit.** An axle in a bush, and nothing else.
3. **The answer goes nowhere.** You find that 0.15 works and then retype `--clearance 0.15` for the
   rest of your life, and which spool and which machine it was for lives only in your head.

---

## One number, checked several ways

The single most consequential decision here, because everything else follows from it.

`ClearanceOffset` insets every face of every part by one figure. There is exactly one clearance in
a build, and the pipeline has no way to apply a different one to a stud than to an axle. So the
extra parts on the calibration plate exist to **validate one number across several kinds of fit,
not to produce several numbers**. You print the plate, find the row where every fit works, and save
that figure.

The alternative — a clearance per kind of fit — was considered and rejected. Acting on it would
require the pipeline to know which features of an arbitrary LDraw mesh are studs and which are
axles. That is feature detection on unconstrained geometry: a larger project than this one, and
arguably larger than the mobile heads.

If no single row satisfies every fit, that is itself the finding, and the sheet says what to do
with it: favour the tightest mating pair, because a joint that will not go together is a failure
and a joint that is slightly loose is a nuisance.

---

## The plate

`calibration --printer A1` writes **one 3MF** into its output folder, with the slicer preset
`Lego2STL.json` beside it, exactly as a build plate now carries one.

**One sheet, not two.** A build plate's folder gets `how-to-print.txt`; a calibration folder
already gets `how-to-use-these.txt`, and writing both into one folder would leave two overlapping
instruction files where the whole reason the note exists is that a folder of near-identical shapes
is confusing without one. So the calibration folder keeps its single sheet under its existing name,
and that sheet grows to carry everything: the print settings composed from the same source a build
sheet uses, then the map of which piece is which, then what to do with the answer. `PrintNotes`
therefore has to offer its settings section to a caller that is writing its own document around it,
rather than only ever writing a whole file.

### What is on it

Three mating pairs, each at all six clearances:

| Pair | Parts | Footprint of each, at true size |
|---|---|---|
| Axle in bush | `3705` Technic Axle 4 + `4265c` Technic Bush | 31.6 × 4.8, 7.2 × 4 mm |
| Stud in tube | `3003` Brick 2 x 2, twice | 16 × 16 mm |
| Pin in Technic hole | `3700` Technic Brick 1 x 2 with hole + `3673` Technic Pin | 16 × 8, 16 × 6.4 mm |

Six pieces at six steps is 36 pieces, about 5 500 mm² of part area, and the tallest is 11.2 mm.
Beside them, **one** `3035` Plate 4 x 8 (64 × 32 mm) at clearance zero: the warping witness. That
is 7 600 mm² in all, against an A1's 60 500 mm² of usable bed — around an eighth of it before
packing losses, so fitting one plate is not a close call. The measurements above were taken from
the tool itself on 2026-09-01, not estimated.

### Why the witness is not part of the matrix

The roadmap named four things the set should test, and one of them is not a clearance test. A wide
plate lifting at its corners is warping — a question about the bed, the first layer and the
temperature, and no clearance value fixes it or changes it. Printing it at six clearances would
spend bed and filament varying something along an axis that does not affect it.

But dropping it would be worse, because it is the check that tells you whether your other readings
mean anything. So it appears once, and the sheet says plainly: **if this plate lifts at its
corners, no clearance reading on this plate is worth anything yet, and the bed comes first.**

### Clearance applies to both halves of a pair

A pair printed at 0.15 has 0.30 mm of gap between the two surfaces. That is not a quirk to correct
for. It is exactly what happens in a real build, where both mating parts are printed by the same
machine at the same setting, and correcting for it here would make the calibration measure
something the build never reproduces.

### Which piece is which

**Nothing is embossed into the geometry.** It is tempting and it is wrong twice: it alters the
surface being measured, and digits at a 0.16 mm layer height are unreliable to read anyway.

Position on the plate says which is which, and the sheet carries the map. The map is **generated
from the placement the packer returned** — row by row, front to back, each row left to right — and
never from the order the items were handed over. `ShelfPacker` sorts by depth, then width, then
label, so an assumed order would be wrong the moment a footprint changes; and clearance changes
footprints, which is the whole point of the plate.

### A part the library has not got

It is left off, the sheet names it and says which fit therefore went untested, and the rest of the
plate is written. Today a missing part aborts the command with an error, which is right when the
output is that one part and wrong for a plate whose value is mostly still there.

---

## The store

A named figure, kept where both the command line and the window can read it.

**Where.** `AppDataDirectory.File("tolerances.json")`, in Core. This is forced rather than chosen:
`UserSettings` is the window's own file and lives in `Lego2STL.Gui`, which the command line does
not reference and cannot see. `AppDataDirectory` is already in Core and already the answer to
"where do the things kept between one use and the next live".

**Unreadable or absent is treated as no presets**, the same forgiving rule `UserSettings` uses, for
the same reason: losing a preference must never stop a run.

**The record is four fields:** the name the user chose, the clearance in millimetres, whether it is
preferred, and when it was saved.

Deliberately **not** a structured printer and material. The roadmap rejected a composed key of
printer + nozzle + material, and adding those as fields recreates it by the back door.
`"eSUN PLA+ black – A1"` already says everything a person needs, it is what Bambu Studio itself
does, and it survives the cases a key cannot express: two spools of the same material that behave
differently, or a machine that has drifted since January.

**At most one preset is preferred**, at any time, and that is an invariant of the store rather than
a convention its callers follow.

---

## How a clearance reaches a build

**Precedence, most specific first:**

1. `--clearance`, given explicitly
2. `--tolerances <name>`
3. the preferred preset
4. nothing, and the build runs at true size as it always has

Explicit always beats remembered. A preferred preset applying without being asked for is a
departure from how this tool has behaved, and it is deliberate: the alternative is knowledge that
exists and goes unused, which is the problem C was created to solve. It follows the catalogue's
preferred shop, which already works this way.

### `--clearance` becomes nullable

Today it is an `Option<double>` defaulting to 0, so `--clearance 0` and not passing it are the same
value. With a preferred preset in play those mean opposite things — "no clearance, thanks" and "use
my saved 0.15". The option becomes nullable, and an explicit zero is a real zero.

### A run says where its number came from

When a preferred or a named preset supplies the clearance, the run says so once in its log and the
manifest records the preset's name beside the value. This is the price of a number that applies
without being asked for, and it is this codebase's existing standard: every other decision the tool
makes is already recorded, including, since A+B, how each part was laid on the bed.

### A named preset that does not exist refuses the run

Listing the names that do exist. It does **not** fall back to zero. A mistyped name that silently
builds at true size is a wasted plate discovered after printing, which is the failure mode this
whole sub-project is about.

---

## The command

The roadmap sketched step 3 as `calibration --save 0.15 --name "…"`, and that is what this design
adopts. It gives `calibration` two modes, which is a real cost and is named here rather than
discovered later:

- **Build the plate.** The default, with the existing `--part`, `--steps`, `--output-dir`,
  `--ldraw-dir`, `--offline`, `--ascii`, plus `--printer` to choose the bed and the preset.
- **Manage what you measured.** `--save <mm>` with `--name <text>`, optionally `--preferred` to mark
  it at the same time; `--list`; `--prefer <name>`; `--forget <name>`. Each of these records or
  reports and exits without building anything.

The flags in the second group are mutually exclusive with building and with each other. **If a
fifth is ever wanted, that is the signal to split them into a command of their own** rather than to
add it.

The calibration sheet ends by printing the exact `--save` line to run once a row has been chosen,
so the command you need is in front of you at the moment you need it — the same reasoning as the
note itself.

---

## The window

**Settings gains a tolerance list**, built like the shops list directly above it: rows carrying a
name, a clearance and a preferred mark, with add, delete, and choose-which-is-preferred.
`ShopRowViewModel` and its `Remember…()` writer are the pattern to follow.

One difference is worth stating because it will look like an inconsistency otherwise: the shops
list persists into `interface.json` through `UserSettings`, and the tolerance list persists into
Core's `tolerances.json`. Same shape on screen, different file underneath, because the command line
has to read one of them and cannot see the other.

**The run options carry the choice as a choice, not as a resolved number.** The window hands Core
either an explicit clearance, or a preset name, or nothing — exactly what the command line hands
it — and Core resolves the precedence. One resolution path, one set of tests, and no way for the
two front ends to disagree about what a preferred preset means.

**The run document offers a build's two files.** `how-to-print.txt` and `Lego2STL.json`, opened the
way the parts list already is. This closes the one item A+B's design left under *Left for the plan* and
never answered. It is small, and it is the difference between those files existing and anyone
finding them.

**The calibration button goes on Settings, beside the tolerance list**, because a calibration is
run in order to fill that list. The alternative was a fourth input kind on Setup, next to Document,
Parts list and Set number — genuinely elegant, since a calibration is a job with a printer and an
output folder and no input file. It was rejected because `InputKind` has parity tests across the
whole options surface, and perturbing them for a job run once per spool is not a good trade.

---

## What this design deliberately does not do

- **It does not emboss the clearance into the geometry.** It would alter the surface being
  measured, and embossed digits at a 0.16 mm layer height are unreliable to read.
- **It does not produce a clearance per kind of fit.** The pipeline would need feature detection on
  arbitrary LDraw geometry to act on one.
- **It does not guess a clearance** when nothing is preferred and nothing was asked for. The
  command's founding refusal stands.
- **It does not record a structured printer and material.** That is the composed key the roadmap
  rejected.
- **It does not measure a fit for you.** Step 2 of the flow is irreducibly physical.
- **It does not touch `ClearanceOffset`**, or change what a build does when no preset is involved.
- **It does not colour-group the calibration plate.** It is one plate of one colour; the grouping
  that makes a build plate a printable job has nothing to do here.

---

## How it is proved

- **The `PlateBuilder` split proves itself by omission.** `PlateBuilderTests` must pass
  **unchanged**. If extracting the packing seam requires a single existing test to be edited, the
  extraction changed behaviour and is wrong. Beside that, one new test that the extracted entry
  point accepts labels that are not part numbers and places all of them.
- **The plate:** six steps by six pieces plus one witness; a part the library has not got is left
  off and named in the sheet rather than aborting the command.
- **The map is generated, not assumed:** tested by packing items whose input order `ShelfPacker` is
  known not to preserve, so a map built from the input order fails.
- **The store:** a round trip; an unreadable file reads as no presets; saving a name that exists
  replaces it; at most one preferred, always.
- **Precedence, table-driven** over explicit clearance, named preset and preferred preset, with
  `--clearance 0` given explicitly asserted as a real zero rather than as "unspecified". A named
  preset that does not exist refuses the run. The manifest records which preset supplied the
  number.
- **The window:** the tolerance list by the route `SettingsTests` already uses for shops; the run
  options through `OptionRoundTripTests` and `OptionParityTests`, which already walk every option;
  the two file commands on the run document.
- **The wording:** every new `TextKey` in both languages, through the completeness check the suite
  already runs.
- **By hand, by a person:** printing the plate and trying the fits. No version of this tool can
  measure an interference fit, and the plan says so rather than pretending.

---

## Left for the plan

- The order of the pieces, given that the `PlateBuilder` split is a refactor that everything else
  on the plate side depends on and should therefore come first, with the suite green before
  anything is built on it.
- Whether `--printer` on `calibration` reuses `PipelineOptions`' own printer option or declares its
  own, since `calibration` does not take the rest of that set.
- What the sheet's map looks like on the page — rows of coordinates, or a drawn grid — bearing in
  mind it is read next to a physical plate by someone holding a part.
- The shape `PrintNotes` exposes so the calibration sheet can compose its settings section, given
  that it writes a whole document today and now needs to be usable as a part of one.
