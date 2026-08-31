# The Plate Arrives Printable — Design

**Date:** 2026-08-31
**Status:** approved 2026-08-31. Ready for an implementation plan.
**Covers:** sub-projects A and B of
`2026-08-31-print-quality-and-mobile-roadmap.md`, taken together.
**Comes from:** a first print that came out badly — parts positioned so that supports could not be
removed, filament spread everywhere, pieces deformed — and the print advice gathered afterwards.

---

## Why A and B are one design

Both answer the same question: how does a part end up sitting on the bed, and what does the slicer
then do with it. A decides what the slicer is told; B decides how the part is lying when it is told
it. Split them and the same question gets answered twice, in two places, by two people who may
disagree. The clearest case: A may say *supports off* only because B has established that the way
these parts are lying does not need them. Asserting it without B would be asserting something
nobody had checked. In the event B checked and had to change nothing, but the checking is what
earns A the claim, and it is now a test rather than a belief.

---

## What a run produces

Three things, for a run that writes plates. A run asked for `--no-plates` produces none of them:
they are about printing a plate, and there is no plate.

1. **A process preset** for the slicer, beside the `.3mf` files.
2. **An instruction sheet**, in the run's own language, covering every setting including the ones
   the preset deliberately does not assert.
3. **The parts already oriented** inside the plates, because B acts before packing — which after
   the measurement below means the plates are unchanged, and the orientation each part was given is
   recorded rather than assumed.

The sheet is the primary deliverable and the preset is a convenience. That ordering matters: the
sheet is a text file that is correct for ever, while the preset depends on Bambu's own preset names
and could stop importing after some future release. When the preset cannot be written, the sheet
still says everything a person needs.

---

## A — the settings travel with the plate

### The preset is thin, and inherits

A Bambu process preset is a small JSON carrying `type`, `name`, `inherits`, `from`, and **only the
values that differ from the base it inherits**. The system preset `0.16mm Optimal @BBL A1` has
eleven keys in total and asserts exactly one setting of its own,
`elefant_foot_compensation: 0.075`. What this project ships takes the same shape:

```json
{
  "type": "process",
  "name": "Lego2STL 0.16mm @BBL A1",
  "from": "User",
  "inherits": "0.16mm Optimal @BBL A1",
  "...": "our differences, and nothing else"
}
```

The base is Bambu's and stays Bambu's; only the differences are ours. That is what keeps the file
small enough to read and stops it going stale in the way a whole copied profile would.

**The layer height is not asserted.** Inheriting from a `0.16mm` base makes it 0.16 mm already.
Choosing the base is more honest than overriding the value, and it means one fewer thing that can
contradict the base it sits on.

### What it may assert, and what it may not

The line falls where the tool's knowledge ends. It knows the print bed, because it packs onto one.
It knows what it built: small interlocking parts whose dimensional accuracy matters more than their
speed. It knows how B oriented them. It does not know the nozzle, the filament, the spool or the
room.

So the preset asserts supports off — because B has just made them unnecessary — a brim,
elephant-foot compensation, slow outer walls and small perimeters, wall count, top and bottom shell
layers, and infill. It asserts no temperature and no volumetric speed. Those belong to a spool, and
a preset that pretends to know a spool will be wrong for somebody on the first day.

**The exact setting keys are Orca's and Bambu's vocabulary, and the plan must verify each one
against a preset the slicer itself wrote** — set the values in Bambu Studio, save a user preset,
and read the file it produces. The OTA presets on a machine carry only deltas, so a key's absence
from them proves nothing, and guessing a key name produces a file that imports with settings
silently missing. This is a verification step, not a research step.

### Which base, for which printer

Measured against a real Bambu Studio installation on 2026-08-31, for the default 0.4 mm nozzle:

| `--printer` | Inherits | Slots per per-extruder setting |
|---|---|---|
| A1 | `0.16mm Optimal @BBL A1` | 1 |
| A1mini | `0.16mm Optimal @BBL A1M` | 1 |
| P1P | `0.16mm Optimal @BBL P1P` | 2 |
| P1S | `0.16mm Optimal @BBL P1P` | 2 |
| X1C | `0.16mm Optimal @BBL X1C` | 2 |
| H2D | `0.16mm Standard @BBL H2D` | 7 |

Three things this table records that are not obvious. The family name is not uniform: the A1, P1 and
X1 lines call the 0.16 mm profile *Optimal*, while the H2 line calls it *Standard*. There is no
`P1S` token at all — the P1S uses the P1P profiles, which is a substitution and should be named as
one in the sheet rather than passed off as a match. And a per-extruder setting — the speeds — takes
a list whose length is **not** the number of extruders and is not the same for every machine: the
P1P and the X1C are single-extruder printers whose profiles list two values, and the H2D lists
seven. A preset that writes one value where its base has seven hands the slicer a vector of the
wrong length, so the count is part of the table and not a constant.

The verification of 2026-08-31 also settled the form of two values. `brim_type` takes one of
`no_brim`, `outer_only`, `inner_only`, `outer_and_inner`, `auto_brim`, `brim_ears`; and
`small_perimeter_speed` is written as a **share of the outer wall speed**, never as an absolute —
every occurrence in every profile the slicer ships, across all ten vendors, is a percentage.

The base name also varies with nozzle: `0.16mm Optimal @BBL A1 0.2 nozzle` exists beside the plain
name. The tool does not know the nozzle, so it targets the default 0.4 mm — and the sheet says so,
because someone running a 0.2 mm nozzle needs to know the preset is not for them.

**When the printer is not in the table, no preset is written**, and the sheet says why. A file whose
`inherits` names a preset that does not exist fails to import, and a failed import is worse than an
absent file: it looks like the tool is broken rather than silent.

### The sheet

Written in the run's language, through `TextKey` like everything else, and covering the whole
starting profile — nozzle and bed temperatures, fan, first-layer speed, outer and inner wall
speeds, top surface and small perimeter speeds, maximum volumetric speed, walls, top and bottom
layers, infill density and pattern, brim, and supports. Everything the preset asserts and
everything it declines to.

It also carries the two things a settings table cannot: the calibration sequence — Flow Dynamics
first, then Flow Rate, per spool — and the statement that these are starting values which the
user's own calibration outranks the moment they have one. Sub-project C is what turns that
calibration into something the tool remembers; until then the sheet is where the knowledge lives.

This follows what `calibration` already does. It writes a note beside its shapes because *"a folder
of near-identical shapes is useless without one, and by the time they are printed the command line
that made them is long gone"*. The same is true of a folder of plates.

---

## B — orientation by what the part is

### Where the kind comes from

`LDrawFile.Title` — *"the description on the first line"* of the part file the run already
downloaded, which reaches `PreparedMesh.Title` and the run's record. LDraw descriptions are
regular: `Brick 2 x 4`, `Plate 1 x 2`, `Technic Axle 4`, `Technic Pin`, `Technic Beam 3 x 5`.

**Not from `PartFact.Category`.** That reads from a Rebrickable bulk download whose
`inventory_parts.csv` alone is 132 MB, which is not ours to redistribute and is never committed;
every caller has to work without it, so most runs have no category at all.

Measured over the 223 parts of run 6324712: 219 carry a title. Of those, 150 begin with `Technic`,
so the kind is frequently the second word, not the first. The distribution is roughly Beam 43,
Tile 21, Plate 19, Brick 14, Pin about 15, Axle about 14, and then a long tail of Cross Block,
Gear, Panel, Connector, Slope, Bar, Bracket, Turntable and one-offs.

A table of the obvious kinds therefore recognises something like three-fifths of a real set. **What
happens to the other two-fifths is a central question of this design, not a footnote.**

### The rules only go where the evidence says they are needed

The overhang measurement of 2026-08-31, once faces resting on the bed were excluded, put Plates and
Panels at the top of the "worst overhang" list and Axles at the top of the "biggest gain by
turning" list. Both are misleading, and instructively so: a plate belongs flat on the bed and
turning it would be wrong, while turning an axle means standing it on end, which is wrong twice
over. The parts the score shouts about are the parts already lying correctly.

So B is not an optimiser. It is a small set of rules that **confirm** what the pipeline already
does — one of them was going to be a correction, and the measurement below took it away, which
leaves a table that turns nothing at all. That is a thinner result than this design set out to get
and it is the honest one: the pipeline was already laying these five kinds down correctly, and what
B adds is that this is now written down, held still by tests, and recorded per part.

| Kind | How it sits | Change from today |
|---|---|---|
| Brick, plate, tile | Hollow underside on the bed, studs up | Confirms current behaviour — **verified** |
| Technic beam | Widest face on the bed | Expected to confirm — the plan checks it |
| Technic axle (cross section) | Horizontal, flat as it lies | Confirms current behaviour — the roll was **measured and rejected**, below |
| Technic pin | Horizontal | Expected to confirm — the plan checks it |
| Anything else | Exactly as the pipeline leaves it today | No change, and recorded |

"Verified" and "expected" are not the same claim and are not written as though they were. Plates
are verified from the reference run's own footprints: `Plate 4 x 8` comes out 128.0 × 64.0 × 9.6 mm
and `Plate 6 x 8` 128.0 × 96.0 × 9.6 mm, which is flat with the studs up. Beams and pins were not
measured, so their rows are predictions, and the plan's first job for each is to confirm the rule
is a no-op before writing it as one. A rule that silently starts moving parts it was supposed to
leave alone is the failure mode this whole table exists to avoid.

The governing rule, stated as a rule because it is the reason the table exists rather than a
consequence of it: **no support ever touches a mating surface** — a stud, an interior tube, a
Technic hole, an axle or a pin. When orientation and support disagree, orientation moves.

### The axle roll: measured on 2026-08-31, and rejected

The axle rule was the one thing here that did not come from a measurement. It came from practice:
an axle lies horizontally, with one point of the cross on the bed. It was also the rotation the
earlier probe could not have found, because it tried only the six axis-aligned orientations on the
argument that *"a part that only improves at 37 degrees is not a part anyone will place by hand"* —
and a `+` cross section is symmetric under the 90° rolls that rule admits, so every roll it tried
was a no-op.

The physical reasoning was that a `+` resting flat has two horizontal arms with their undersides in
the air, while the same cross rolled 45° into an `×` rests on one arm and its lower faces fall away
at 45°, which is the self-supporting limit.

It was checked against the six plain axles of run 6324712 — 3705, 32073, 3706, 3707, 3737 and
3708 — as the share of surface area on faces pointing more than 45° below horizontal, excluding the
face the part rests on. **The rule ships only if the rolled figure is lower for all six. It is
lower for four:**

| Part | Title | Flat | Rolled 45° |
|---|---|---|---|
| 3705 | Technic Axle 4 | 24.200% | 23.911% |
| 32073 | Technic Axle 5 | 24.359% | **31.652%** |
| 3706 | Technic Axle 6 | 24.465% | 24.272% |
| 3707 | Technic Axle 8 | 24.598% | 24.453% |
| 3737 | Technic Axle 10 | 24.678% | **32.314%** |
| 3708 | Technic Axle 12 | 24.732% | 24.635% |

**So the rule does not go in, and the axle row above confirms the pipeline instead.**

The reason it fails is worth more than the verdict. Rolled 45°, an axle's underside faces sit at
*exactly* the 45° limit, so whether they count as overhangs is a tie the score has to break, and it
breaks it differently for different meshes — which is the whole 7-point spread above. Moving the
limit two degrees either way shows it plainly: at 43° every rolled axle scores about 8.9% against
24.2% flat, and at 47° every one of them scores about 39.6% against the same 24.2%. Flat, by
contrast, reads 24.2% at all three limits, because a flat axle's overhanging faces are horizontal
and nowhere near the threshold.

A prediction that reverses sign under a two-degree change in a threshold has not been confirmed by
this measurement; it has been shown to be outside what the measurement can settle. Turning every
axle in every run is not warranted on that. The claim can be revisited by printing one axle both
ways, which is evidence of a kind no geometric score is.

### Three smaller decisions

**A title that matches nothing** leaves the part exactly as the pipeline leaves it today, and the
run records that no rule applied. Recording it is the point: the set of unmatched titles can then be
read off a real run and the table grown from evidence, instead of by writing rules for kinds nobody
has printed.

**`~Moved to 3023b`.** Four parts in the reference run carry an LDraw redirect stub as their title
rather than the description of the part actually built. The mesh is right — the pipeline follows
the redirect — but the recorded title is the stub, and B reads exactly that field. The resolved
part's description is what should be recorded.

**Orientation decides before fitting.** Lot B's `FittingScale` computes the largest scale at which
everything fits, and turning a part changes its footprint. Orientation is settled first, from what
the part is, and the fitting scale then measures whatever results. Turning is about whether a part
can be printed at all; scale is about whether it fits the bed, and a part printed badly at a size
that fits is not a better outcome. On this set the question is close to theoretical anyway: a
9.6 mm cross rolled 45° becomes about 13.6 mm across.

The orientation each part was given is recorded per part, like every other decision the tool makes,
so a person can see it and disagree with it.

---

## What this design deliberately does not do

- **It does not write Bambu's project format into the 3MF.** `ThreeMfWriter` was written without a
  library on purpose — *"no maintained .NET library for it exists outside a commercial product…
  writing it directly is less code than binding to one would be, and leaves nothing to go stale"* —
  and embedding `project_settings.config` would trade that away for something that breaks at the
  next release. The plate stays a plate any reader understands.
- **It never bakes elephant-foot compensation into the STL.** The first layer is squashed by the
  printer; correcting for that belongs to the slicer on the machine doing the squashing.
  `ClearanceOffset` already insets faces, so extending it this way is a plausible-looking mistake,
  and it would permanently deform every shape the tool writes for one machine's behaviour on one
  day.
- **It does not orient by score.** Measured and rejected; the numbers are in
  `2026-08-30-lot-d-what-is-left.md` under item 10.
- **It does not ship a filament preset.** Temperatures and volumetric speeds go in the sheet as
  advice, not in a file as an assertion.

---

## How it is proved

- **The kind reader**, table-driven over the 219 real titles from run 6324712, including the
  `Technic`-prefixed majority where the kind is the second word, and including the redirect stubs.
- **The axle roll** was measured on the six real axles above and rejected, so there is nothing left
  to prove about it and no code to prove it against.
- **Every rule changes nothing**, which after that measurement is the whole table. A plate, a tile,
  a beam, a pin and an axle come out of the pipeline in the same orientation with the rules on as
  without them, corner for corner. This is the test that stops B quietly becoming an optimiser
  later, and it is now the only kind of test B has.
- **An unmatched title** leaves the mesh byte-for-byte as it was, and is recorded as unmatched.
- **The preset**, for its JSON shape, for the inherited name of each printer in the table, and for
  writing no file at all when the printer is not in it.
- **The sheet**, through the completeness check the suite already runs over every `TextKey` in both
  languages.
- **By hand, by a person:** importing the preset into Bambu Studio and confirming it takes, and
  printing a plate. Neither can be done headlessly, and the plan says so rather than pretending.

---

## Left for the plan

- Verifying every setting key against a preset Bambu Studio itself wrote, before any of them are
  committed to code.
- Where in the run folder the two files go. Beside the plates is the obvious answer, since
  `RunLayout` already names a plate directory and the catalogue's plate scan matches only `*.3mf`,
  so neighbours are harmless.
- Whether the window offers the sheet and the preset the way it offers the parts list and the log.
