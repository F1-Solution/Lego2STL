# Turning a Part to Need Fewer Supports — Spike Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to run this
> spike task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Answer one question — can the run tell, from the geometry it already holds, that a part
would print better standing up? — and report the answer. **The output of this plan is a
recommendation, not a feature.** Item 10 of the reported list; see
`docs/superpowers/specs/2026-08-29-reported-items-and-lots.md`.

**Architecture:** A throwaway probe over the shapes of run `6324712`, scoring each part's
overhang at a handful of rotations and comparing the best against the one the pipeline currently
produces. Nothing it builds is kept.

**Tech Stack:** C# / .NET 10, the existing `Lego2STL.Core` geometry types, a scratch console
project outside the repository.

**Spec:** none. This is the measurement that decides whether a design is worth writing, and
`docs/superpowers/specs/2026-08-30-lot-d-what-is-left.md` records why.

## Global Constraints

- **Nothing here is committed to `src/` or `tests/`.** The probe lives in the scratchpad and is
  deleted at the end. The only artefact that survives is the finding, written into the lot D
  record.
- The probe reads shapes the pipeline already wrote. It must not need a network, an LDraw
  library, or a rebuild of the run.
- A negative answer is a good answer, and is reported as plainly as a positive one. Do not
  soften it, and do not build the feature anyway.
- Stop at the end of Task 4. Designing the feature is a separate piece of work that begins with
  brainstorming, whatever the numbers say.

---

### Task 1: Score how much of a shape overhangs

**Files:**
- Create: `<scratchpad>/turning/Program.cs` and a minimal `.csproj` referencing
  `src/Lego2STL.Core/Lego2STL.Core.csproj`

**Interfaces:**
- Consumes: `IndexedMesh`, `IndexedMesh.ToTriangle`, `Triangle.Normal()`, `StlWriter` for reading
  back what the run wrote.
- Produces: a number per shape per rotation, printed. Nothing another task consumes as code.

- [ ] **Step 1: Read a shape back**

Write a probe that loads one `.stl` from the reference run's `stl/` folder into an `IndexedMesh`.
The run writes binary STL by default, so read the binary form: 80 bytes of header, a
little-endian `uint` count, then 50 bytes per triangle — a normal and three vertices as 12
`float`s, and two trailing bytes to ignore.

- [ ] **Step 2: Score the overhang at the orientation the run produced**

For every triangle, take the outward normal and the area. A face overhangs when its normal points
downward more steeply than a slicer will bridge; the usual threshold is 45 degrees from vertical,
which in these terms is `normal.Z < -cos(45°)` once the shape is standing the way
`MeshPipeline.StandUp` leaves it.

Print, per part: total area, overhanging area, and the fraction. Sort the parts by that fraction
and print the ten worst.

```csharp
static (double Total, double Over) Score(IndexedMesh mesh, float cosLimit)
{
    double total = 0, over = 0;

    foreach (var indexed in mesh.Triangles)
    {
        var t = mesh.ToTriangle(indexed);
        var raw = Vector3.Cross(t.B - t.A, t.C - t.A);
        var area = raw.Length() / 2f;

        if (area <= 0)
        {
            continue;
        }

        total += area;

        if (Vector3.Normalize(raw).Z < -cosLimit)
        {
            over += area;
        }
    }

    return (total, over);
}
```

- [ ] **Step 3: Sanity-check the score against something known**

An axle (`3705`) lying along the bed should score near zero overhang; a panel with a large flat
underside should score high. If those two do not come out in that order, the score is wrong and
everything after it is meaningless — fix it before going on.

- [ ] **Step 4: Report the distribution**

Print how many of the run's 175 shapes have an overhanging fraction above 5%, 10% and 25%. This
says how much of the set the question is even about. If almost nothing overhangs, the answer to
item 10 is already no, and Tasks 2 and 3 are unnecessary — say so and skip to Task 4.

---

### Task 2: See whether turning helps, and by how much

**Files:**
- Modify: the probe

**Interfaces:**
- Consumes: the score from Task 1.
- Produces: for each part, the best rotation found and the overhang it leaves.

- [ ] **Step 1: Try the six axis-aligned orientations**

Rotate the mesh by 90 degrees about X and about Y to reach the six ways a box can sit, score each,
and keep the best. Six, not a search: a part that only improves at 37 degrees is not a part
anyone will place by hand, and a printer's own slicer offers the same six.

```csharp
static IEnumerable<(string Name, Matrix4x4 Turn)> SixWays()
{
    yield return ("as built", Matrix4x4.Identity);
    yield return ("on its back", Matrix4x4.CreateRotationX(MathF.PI));
    yield return ("on its face", Matrix4x4.CreateRotationX(MathF.PI / 2));
    yield return ("on its back face", Matrix4x4.CreateRotationX(-MathF.PI / 2));
    yield return ("on its side", Matrix4x4.CreateRotationY(MathF.PI / 2));
    yield return ("on its other side", Matrix4x4.CreateRotationY(-MathF.PI / 2));
}
```

- [ ] **Step 2: Report the improvement, not the score**

For each part print: the overhang as built, the best overhang, which turn achieved it, and the
difference. Then print how many parts improve by more than 5 percentage points, and how many by
more than 20.

- [ ] **Step 3: Print what turning costs**

For each part that improves, print its footprint as built and its footprint turned. A part that
sheds supports and stops fitting the plate has not been improved — Lot B's fitting scale computes
the plate's limits and this is where the two features collide. Count how many of the improved
parts grow past the 256 x 256 mm bed.

---

### Task 3: Check the score against a real slicer

**Files:**
- none: this task is done by hand, with the numbers written down

**Interfaces:**
- Consumes: the ranking from Task 2.
- Produces: agreement or disagreement between the score and a slicer.

- [ ] **Step 1: Take the three parts the score says gain most**

Export each in both orientations — as built and as the probe recommends.

- [ ] **Step 2: Slice all six and record the support material**

Load each into the slicer the printer actually uses, with supports on and everything else left at
its defaults, and write down the support filament each reports.

- [ ] **Step 3: Say whether the score predicted the slicer**

The score is useful only if the orientation it prefers is the orientation that uses less support
material in all three cases. Two out of three is not a pass: a suggestion that is wrong a third
of the time is worse than no suggestion, because someone will act on it.

---

### Task 4: Report, and stop

**Files:**
- Modify: `docs/superpowers/specs/2026-08-30-lot-d-what-is-left.md`

- [ ] **Step 1: Write the finding into the lot D record**

Under item 10, replace "the question the spike must answer first" with what was measured: how
many parts overhang at all, how many improve by turning, how many would then not fit the plate,
and whether the slicer agreed. Give the numbers, not an impression.

- [ ] **Step 2: Make a recommendation in one paragraph**

One of three: build it (and what the design would have to settle — chiefly which of turning and
the fitting scale decides when they disagree); build nothing, because too little of a real set
overhangs to be worth it; or the score does not predict the slicer, so this approach is a dead
end and any future attempt should start from the slicer's own numbers instead.

- [ ] **Step 3: Delete the probe**

```bash
rm -r <scratchpad>/turning
```

- [ ] **Step 4: Commit the finding**

```bash
git add docs/superpowers/specs/2026-08-30-lot-d-what-is-left.md
git commit -m "docs: whether turning a part to need fewer supports is worth doing"
```

---

## Notes for whoever executes this

- The temptation in a spike is to keep the code because it works. This code reads STL files by
  hand and scores triangles crudely; it is fine for answering a question and not fine for
  shipping. If the answer turns out to be yes, the feature is written from scratch, with tests,
  from a design.
- Task 1's Step 4 may end the spike early, and that is a success rather than a shortcut.
- Record `PHASE:LOT-D WAVE:10 STATUS:complete TS:<ISO-8601-UTC>` in `PROGRESS.md` when the
  finding is committed, whatever the finding was.
