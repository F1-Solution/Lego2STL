# Print Quality And Mobile — A Decomposition

**Date:** 2026-08-31
**Status:** decomposition approved 2026-08-31. Four sub-projects, each to get its own spec and
its own plan. Nothing here is approved design for any of them; this file settles what the pieces
are, what order they go in, and what each one's spec must answer.
**Comes from:** the reported items closed as lots A to D, plus a conversation about a first print
that came out badly — the parts were positioned so that supports could not be removed, filament
was spread everywhere, and pieces deformed. That conversation is the reason A and B exist and the
reason they come first.

---

## Why this is four projects and not one

The request was for Android and iOS. Investigating it turned up two things that matter more and
cost far less: the plates this tool writes arrive in a slicer with no print settings at all, and
the orientation each part is given is decided by nothing. Both were behind the ruined print, and
neither has anything to do with a phone.

So the phone comes last, and the two things that make the next print work come first.

| | Sub-project | Depends on | What it is for |
|---|---|---|---|
| **A+B** | The plate arrives printable | — | The ruined print |
| **C** | Calibration, and remembering its answer | A+B | Fits, and not retyping a number for ever |
| **D** | A recogniser on every platform | — | Scanned instruction books off Windows |
| **E** | Android and iOS | D, and A-C settled | What was asked for |

---

## A+B — The plate arrives printable

One sub-project, not two, because both answer the same question: how does a part end up sitting on
the bed, and what does the slicer then do with it. Separating them would mean deciding twice.

### A — the settings travel with the plate

**What is wrong now.** `ThreeMfWriter` writes geometry and colour. The colour is done carefully —
written twice, in the two places readers look, so a plate opens in Bambu Studio already coloured.
Nothing else is written. So the plate arrives carrying whatever process preset happened to be
selected last: supports on, whatever brim, whatever layer height. A plate that looks right and
prints wrong.

**What its spec must settle, and the recommendation.** Not to write Bambu's own project format
into the 3MF. `ThreeMfWriter` was deliberately written without a library — *"no maintained .NET
library for it exists outside a commercial product… writing it directly is less code than binding
to one would be, and leaves nothing to go stale"* — and embedding `project_settings.config` would
trade exactly that away for something that breaks at the next Bambu Studio release. The plate
should stay a plate any reader understands, and the settings should ship beside it as a **process
preset the user imports once**: supports off, brim automatic, elephant-foot compensation, a layer
height, and the slow outer walls that dimensional accuracy needs. The report says to import it.

The other thing to settle is restraint, and where the line falls. This tool knows the print bed,
because it packs onto one, and it knows what it built — that these are small interlocking parts
whose accuracy matters more than their speed, and that it oriented them so no support is needed.
It does not know the nozzle, the filament, the spool, or the room. So a preset may say supports
off, because B has just made them unnecessary; it may say slow outer walls and a brim, because
those follow from what was built. It may not say a nozzle temperature. The preset also has to
arrive labelled as a starting point the user then owns, because the moment they calibrate — which
is C — their own figures outrank it.

**An explicit non-goal.** Elephant-foot compensation never enters the STL. The first layer is
squashed by the printer, and correcting for that is the slicer's job on the machine that will do
the squashing. `ClearanceOffset` already insets faces, so extending it to "compensate the first
layer" is a plausible-looking mistake somebody will eventually propose. It would permanently
deform every shape the tool writes, for one machine's behaviour on one day.

### B — orientation by what the part is

**What is wrong now.** `MeshPipeline.StandUp` turns every shape from the source's axes onto a bed
and `SitOnBed` drops it onto zero. That is a convention, not a decision: nothing asks whether
*this* part wants to lie that way. An axle laid with the arms of its cross horizontal needs
support under both arms; the same axle rotated a quarter turn does not.

**Why not the obvious approach.** Scoring overhang area over six orientations and keeping the best
was measured on 2026-08-31 over the 175 shapes of run 6324712 and is a dead end: it recommends
turning 95 of them, and 50 of those end up taller than three times their narrowest footprint side.
For a 12L axle its advice is to stand 191 mm of part on a 9.6 × 9.6 mm base. The full numbers are
in `2026-08-30-lot-d-what-is-left.md` under item 10. Independently, the print conversation says the
same thing from experience: a Technic pin goes horizontal, never vertical, because standing it up
gives it a base too small and loses precision along the layers.

**The approach that does work.** A small table keyed on what kind of part it is:

| Kind | How it sits | Why |
|---|---|---|
| Brick, plate | Hollow underside on the bed, studs up | Mating surfaces stay clean and the interior tubes need no support |
| Technic axle | Horizontal, long axis along X or Y | A vertical base is tiny; horizontal preserves the cross profile |
| Technic pin | Horizontal | Vertical costs tolerance and strength along the layers |
| Technic beam | Widest face on the bed | Most bed contact, and the holes stay round |

The governing rule, which is worth stating as a rule rather than leaving implicit: **no support
ever touches a mating surface** — a stud, an interior tube, a Technic hole, an axle or a pin. When
orientation and support disagree, orientation moves.

**Where the kind comes from.** Not from the Rebrickable dump. `PartFact(Category, Material)` reads
from a bulk download whose `inventory_parts.csv` alone is 132 MB, which is not ours to
redistribute and is never committed — every caller has to work without it, so most runs have no
category at all. The kind comes instead from `LDrawFile.Title`, *"the description on the first line"*
of the part file the run already downloaded, which flows through `LDrawMeshBuilder` into
`PreparedMesh.Title` and into the run's record. LDraw descriptions are regular: `Brick 2 x 4`,
`Plate 1 x 2`, `Technic Axle 4`, `Technic Pin`, `Technic Beam 3 x 5`. Reading a kind out of that
costs nothing and needs no network the run was not already using.

**What its spec must settle.**

- A title that matches nothing in the table. The honest answer is to leave the part exactly as the
  pipeline leaves it today and record that no rule applied, so the set of unmatched titles can be
  read off a real run and the table extended from evidence rather than guesswork.
- The interaction with the fitting scale. Turning a part changes its footprint, and lot B's
  `FittingScale` computes the largest scale at which everything fits. A part that fits lying down
  may not fit turned. Which of the two decides has to be written down; on the reference run none
  of the improved parts stopped fitting a 256 × 256 mm bed, so this is a rule to state rather than
  a problem to solve.
- Whether the chosen orientation is recorded per part in the run's record. It probably should be:
  it is a decision the tool made that a person may want to overrule, and everything else the tool
  decides is already recorded.

---

## C — Calibration, and remembering its answer

**What is wrong now.** `lego2stl calibration` already does the hard half well. It writes an axle
and the bush that runs on it — *"a fit is a property of two parts and not of one"* — at clearances
from 0.00 to 0.25 mm in steps of 0.05, with a note beside them explaining what to do, because *"a
folder of near-identical shapes is useless without one"*. It refuses to offer a default clearance,
on the grounds that the figure being looked for is smaller than the difference between two machines
of the same model. All of that is right, and the print conversation independently recommends
almost exactly this matrix.

Three things are missing.

1. **The output is loose STLs.** One file per part per step, to be arranged in the slicer by hand —
   by a tool whose whole other output is a packed plate.
2. **The set tests one kind of fit.** An axle in a bush. Not whether a stud enters a hole, not
   whether a wide plate lifts at the corners, not whether a Technic hole closes up.
3. **The answer goes nowhere.** You find that 0.15 works and then retype `--clearance 0.15` for
   the rest of your life, and the knowledge of which spool and which machine it was for lives only
   in your head.

**The flow it becomes.** Four steps, of which the second is irreducibly physical.

1. `lego2stl calibration --printer A1` builds the fuller set — the existing axle and bush, plus a
   brick 2x2, a plate 2x4 and a piece with a Technic hole — at each clearance, packs them onto a
   bed with the packer the tool already has, and writes **one 3MF**, with A's process preset beside
   it. One file to open and print.
2. You print it and try the fits. No version of this tool can measure an interference fit for you.
3. `lego2stl calibration --save 0.15 --name "eSUN PLA+ black – A1"` records the figure as a named
   tolerance preset.
4. `lego2stl build parts.csv --tolerances "eSUN PLA+ black – A1"` takes the clearance from the
   preset. `--clearance` still wins when given explicitly, because an explicit number should always
   beat a remembered one.

**A name, not a composed key.** The preset is identified by a name the user chooses, not by
printer + nozzle + material assembled into a key. It is what Bambu Studio itself does, it is what
the print conversation recommends, and it survives the cases a fixed key cannot express — two
spools of the same material that behave differently, or a machine that has drifted since January.

**What its spec must settle.**

- Where a preset lives so that the command line and the window read the same one. `UserSettings`
  is the window's own today.
- The plate does not emboss its clearance value into the geometry. It is tempting, and it would
  alter the surface being measured; embossed digits at a 0.16 mm layer height are unreliable
  besides. Position on the plate plus the instruction sheet says which row is which.
- Which parts make up the set, and what happens when the LDraw library has not got one of them.

---

## D — A recogniser on every platform

**What is wrong now.** Reading a page that carries no text needs `Windows.Media.Ocr`. It sits
behind `IOcrEngine`, so the design is already right: off Windows there is simply no recogniser, and
a run that needs one says so and stops. That means Linux and macOS cannot read a scanned
instruction book today, and a phone would not be able to either.

A typeset book is a different matter and already works everywhere: `ReadPrintedCatalogue` takes
the element numbers straight out of the page's own text, with no recogniser involved. The
reference document `6324712.pdf` is one of those, which is why the tool has been useful on it from
a Linux build.

**The approach.** One native recogniser per platform behind the interface that already exists:
ML Kit on Android, Vision on iOS, `Windows.Media.Ocr` where it is. Best quality on each platform
and nothing to ship, at the cost of two more implementations. Linux stays uncovered, deliberately.

**What was ruled out, so it is not proposed again.** Tesseract, for every platform at once. It was
investigated on 2026-08-31 and is not available: `Tesseract` (charlesw) ships no native runtimes at
all, `TesseractOCR` puts Windows DLLs loose in `lib/`, and `Xamarin.Tesseract` stopped at 0.3.4 in
the Xamarin era. Reaching Android and iOS would mean cross-compiling `libtesseract` and
`libleptonica` for four architectures ourselves and shipping some 15 MB of trained data — a native
toolchain project larger than the mobile application it would serve, and the same kind of thing
this repository already declined once when a `.pkg` could not be assembled off a Mac.

An ONNX Runtime model was the other candidate and remains the fallback: `Microsoft.ML.OnnxRuntime`
does ship `net9.0-android35.0` and `net9.0-ios18.0`, so it is genuinely portable, at the cost of
choosing and shipping a model and writing detection and recognition.

**What its spec must settle.** Chiefly the testing. Headless CI cannot run ML Kit or Vision, so
what is proved automatically and what is left to a person on a device has to be decided rather
than discovered. The existing suite covers the reading pipeline through `IOcrEngine`, which is the
seam a fake sits in; what it cannot cover is whether the real recogniser reads a real page.

---

## E — Android and iOS

**What is already possible, which is more than expected.** `PDFtoImage` already targets
`net10.0-android36.0` and `net10.0-ios26.0` and brings the right PDFium and SkiaSharp natives with
it, so rendering pages works on a phone. `OpenScadRunner` — the one thing here that launches an
external process — belongs to the command line's `bricks` command and not to the pipeline, so it
is not in the way. Reading a typeset book, building a parts list, generating shapes and packing
plates can all happen on a phone.

**What has to be built.**

- **A shared library and thin heads.** `Lego2STL.Gui` is the desktop application itself today: a
  `WinExe` with its own entry point. It has to become a library holding the views and view models,
  with a desktop head, an Android head and an iOS head over it.
- **Somewhere to write.** `RunLayout` puts a run's folder beside the input file. On Android and iOS
  the input arrives from a document picker and the application is sandboxed, so "beside the input"
  cannot be honoured. A run needs a home in application storage, and the user needs a share sheet
  to get files out — `Desktop.Open` on a folder is the only route today, and there is no folder to
  open on a phone.
- **A layout that fits a phone.** `MainWindow` is a `DockPanel` with a sidebar fixed at 200 px,
  designed at 1040 × 720 with a minimum of 820 × 560. On a 360 dp screen that is unusable; the
  sidebar has to collapse. The catalogue's 264 px cards already cope, one per row.
- **The CI jobs.** An APK for Android. For iOS, a simulator build only: there is no Apple Developer
  Program membership yet, so no certificate and no provisioning profile, so nothing installable on
  a device. The signing step is written and left switched off rather than omitted, so it becomes a
  matter of adding secrets when the membership exists.

**What its spec must settle.** How far the project structure moves, since splitting the GUI touches
every view file's project; whether the storage abstraction is a seam in Core or a service the heads
provide; and what a phone does about a run that wants an 80 MB LDraw library.

---

## Order, and why

A+B first: it is the only one that changes the outcome of the next print, it needs no new platform,
and it is the smallest. C second, because it reuses wherever A+B decide presets live. D third,
independently useful the day it lands — it is what lets a Mac or a Linux machine read a scanned
book. E last, because it is the largest, because D makes it worth having, and because it must not
re-litigate A to C while also inventing three new platforms.

Each sub-project gets its own spec and its own plan, in that order, and is finished before the next
begins.
