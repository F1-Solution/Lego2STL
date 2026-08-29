# Repair that repairs, a scale that fits, and a number you can buy a part with

**Date:** 2026-08-29
**Status:** approved design, not yet implemented
**Covers:** items 2, 11 and 13 of the reported list — "Lot B".
**Follows:** Lot A (`3ad05ea`, `bb12cc2`, `f798a53`), which fixed seven reported defects and is
merged.

## The problem, as reported

> *"Nonostante `--no-repair` è false di default molti pezzi non sono stati riparati (o almeno
> così diceva l'errore nel catalogo dei pezzi)."*

> *"Nel report ho visto `<codice> misura AxB e non entra in un piano`. In questo caso nel
> catalogo pezzi deve riportare il problema e deve offrire la possibilità di cambiare scala a
> tutto il set e ripartire."*

> *"Nel catalogo aggiungere una dropdown per poter cambiare la visualizzazione dei codici dal
> formato BrickLink a quello Lego."*

The first turned out not to be the defect it looked like. The rest of this document rests on
measurement rather than on the report's wording.

## Measured facts this design rests on

The 175 distinct shapes of run `6324712` were rebuilt from the local `complete.zip` and measured
at every stage of `MeshPipeline`. The run itself reported 123 closed of 175.

| Checked | Result |
|---|---|
| What "not closed" means | `MeshQuality.IsClosed` is `OpenEdgeCount == 0 && OverusedEdgeCount == 0`. Both, not just holes. |
| The 52 shapes reported unrepaired | **19** have *no holes at all* — they fail only on overused edges. **33** have holes. **0** have holes without also having overused edges. |
| Where the overused edges come from | **The fill itself.** Measured before repair, `OverusedEdgeCount` is 0 on every part sampled; after `BoundaryFill` it is 2–98. The source geometry has none. |
| Why the fill makes them | `BoundaryFill.Cover` fans from a new centre point, so edge `centre→loop[i]` is shared by the two faces either side of it. When a loop visits the same vertex twice — two gaps meeting at a point — that edge lands on **four** faces. |
| Whether a more tolerant weld closes the remaining holes | Of the 33 with holes, **8** close completely somewhere in 0.005 → 0.1 LDraw units. **25** do not close at any tolerance tried. |
| What the catalogue says about the 19 | `UiWarningNotClosed` — *"Questa forma ha spigoli aperti"* — to parts that have none. There is no wording for the other defect. |
| What the manifest records | `openEdgeCount` only. `OverusedEdgeCount` is measured, then dropped, so no front end can tell the two defects apart. |
| Where oversized parts go | `PlateBuildResult.Skipped`, as **already-formatted strings**. Not carried into `RunManifest` at all — the catalogue cannot know which parts were left off. |
| Whether the packer rotates a part to fit | **No.** `ShelfPacker` compares `Footprint.X` against `Bed.Width - 2 * Margin` and `Footprint.Y` against the depth, as they stand. |
| Whether the element number survives a run | **No.** `PrintedCatalogue` reads it, `ElementLookup` turns it into a part and a colour, and it is dropped there. `CatalogueReading` and `PartEntry` do not carry it. |
| The parts-list CSV schema | Six columns, fixed by decision 2 of `PLAN.md`. Its `Codice Lego` column holds the **part** number, not an element number. |

Two consequences follow and are not negotiable within this design:

- **Item 2 is not a weakness, it is two defects.** A stronger repair alone would leave 19 parts
  still reported as broken while being solid, because nothing about them is a hole.
- **The catalogue cannot tell the truth about a shape until the manifest records both counts.**

## Part 1 — Repair that repairs

### 1.1 The fill stops undoing its own work

`BoundaryFill.Loops` walks free edges into closed loops. A loop that returns to a vertex already
on the path is not one gap, it is two gaps touching at a point. Covering it as one produces the
overused edge measured above.

The walk therefore keeps the position of each vertex on the current path. When it arrives at a
vertex already on the path at position *p*, everything from *p* onward is detached as a closed
sub-loop and covered on its own; the walk continues from the shortened path.

This invents nothing that the current code did not already invent — the same triangles cover the
same area — but each fan gets its own edges.

**Guarded by:** a mesh whose free edges form a figure of eight. Today one cover and two overused
edges; after, two covers and none. Written before the change, and made to fail first.

### 1.2 A more tolerant weld, only where it is needed

After the fill, a shape that is still not closed is prepared again *from the original triangles*
— weld, drop degenerates, split seams, fill — at a larger welding tolerance, stopping at the
first that closes it. Re-welding the already-welded mesh would compound the tolerance rather
than apply it, which is a different and worse operation.

The rungs are the ones measured to matter, in order: **0.005, 0.02, 0.05, 0.1** LDraw units,
starting from whatever tolerance the run asked for and skipping any rung at or below it. The
ceiling of 0.1 units is **0.04 mm**, below what a 0.4 mm nozzle can resolve, so nothing that
closes this way is deformed by having done so.

Three properties this must have, in order of importance:

1. **A shape already closed is never re-prepared.** It cannot change by a single vertex, so no
   part that works today can regress.
2. **The escalation stops at the first tolerance that closes the shape** — not the largest.
3. **`--no-repair` turns it off**, as it turns off the fill. It is repair, and it is opt-out by
   the same switch, not a new one.

The tolerance that succeeded is recorded, so the report can say which shapes needed it.

**Guarded by:** a part measured to need it (`87408`: 94 open edges at the default, 30 at 0.005)
and a part measured not to (`18654`, closed already) — the second asserting the mesh is
byte-identical with the escalation available.

### 1.3 The record carries both counts, and the catalogue says which

`ManifestPart` gains `OverusedEdgeCount` and the tolerance that closed the shape. Both are
nullable, meaning *not measured* — the part produced no shape — rather than measured as zero,
matching the existing convention for `IsClosed` and `OpenEdgeCount`.

The catalogue gains a second wording. The two are not interchangeable:

| Condition | Wording |
|---|---|
| `OpenEdgeCount > 0` | holes in the surface — the existing `UiWarningNotClosed` |
| `OverusedEdgeCount > 0`, no holes | surfaces that pass through each other — new |

A run reopened from before this change has no overused count. It says what it knows and does not
guess: the absence of the figure is not evidence of zero.

### 1.4 What success looks like

`6324712` goes from **123 closed of 175**. The projection from measurement is ~150; the number
that will be reported is the one measured on the real run afterwards, not this estimate. The 25
parts measured as closing at no tolerance will still be open and will still say so.

## Part 2 — A scale that fits

### 2.1 Oversized parts become data

```
SkippedPart(string PartNumber, float Width, float Depth, float Height, bool TooTall)
```

replaces the formatted strings in `PlateBuildResult.Skipped`. `RunReport` prints the same
sentence as today, built from those numbers, so the report does not change.

### 2.2 The run works out the largest scale that would fit

For each part, the factor that would bring it inside the bed is
`min(usableWidth / x, usableDepth / y, height / z)`, using the same `usableWidth` the packer
uses — `Bed.Width - 2 * Margin` — because a suggestion the packer would then reject is worse
than no suggestion.

The run's largest useful scale is the smallest such factor across all parts, times the scale the
run used. It is recorded in the manifest as one number, so a reopened run needs no size parsing
and cannot disagree with what the run itself computed.

Rounded **down** to a whole percent. A suggestion that overshoots by a rounding error is a
suggestion that fails.

**Guarded by:** the measured case. `46891` at 200% is 304 × 184.8 mm against an A1 bed, so the
answer must be below 200 and must make the part fit when applied — asserted by re-packing at the
suggested scale, not by comparing to a number written into the test.

### 2.3 The catalogue offers the way out

An oversized part says so on its own card. Above the cards, when there is at least one, a band:
*"N parts do not fit the plate. The largest scale that fits is X%."* with a button that starts
again from this run's parts list at that scale.

It reuses the path `ContinueFromPartsList` already takes — `Kind = PartsList`, `InputPath =`
the run's own CSV — so the new run lands in the same folder rather than scattering a second copy,
which `RunLayout.For` already guarantees.

Nothing happens without the button being pressed. A scale the user chose is not quietly changed.

## Part 3 — A number you can buy a part with

### 3.1 The element number survives the run

`CatalogueReading` and `PartEntry` gain a nullable element number; `PrintedCatalogue` already
reads it and `ElementLookup` already resolves it. `ManifestPart` records it.

Nullable throughout, because it genuinely does not exist for two of the three input kinds.

### 3.2 The CSV does not change

Six columns, as `PLAN.md` decision 2 fixes them. A seventh would break every list already
written and every tool reading one.

The consequence is deliberate and must be visible rather than papered over: **a run started from
a CSV or from a set number has no element numbers**, and the catalogue says so when that view is
chosen instead of showing blanks that look like missing data.

### 3.3 The menu

Two choices in the catalogue — BrickLink number, LEGO element number — remembered between
sessions alongside the other interface preferences in `UserSettings`.

## What this design does not do

- It does not make the 25 hard parts closed. They are reported honestly and left alone.
- It does not add a *Repair* button. With 1.1 and 1.2 in place there is nothing left to ask of a
  part that the run has not already tried, and a button that usually fails is worse than none.
  This reverses the first answer given during brainstorming, on the strength of the measurement
  that followed it.
- It does not rotate parts to make them fit. That is item 10, still a spike.
- It does not touch the purchase links or the non-printable parts. That is Lot C.

## Order of work

1. Part 1 first, and 1.1 before 1.2: until the fill stops making overused edges, the escalation
   would be tuned against a moving measurement.
2. Part 2 and Part 3 are independent of Part 1 and of each other.
3. The closing measurement on `6324712` is taken after Part 1 and reported.
