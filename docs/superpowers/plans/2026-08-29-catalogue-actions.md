# Catalogue Actions Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make mesh repair actually close the shapes it can, tell the truth about the ones it
cannot, offer a way out when a part is too big for the plate, and keep the LEGO element number
so a part can be looked up by the number printed in the instructions.

**Architecture:** Three independent strands over the existing pipeline. Strand 1 fixes a defect
in `BoundaryFill` that manufactures the very "overused edge" that makes a filled shape count as
unclosed, then adds a bounded weld-tolerance escalation for shapes still open. Strand 2 turns
`PlateBuildResult.Skipped` from formatted strings into data, computes the largest scale that
would fit, and offers a re-run from the catalogue. Strand 3 threads the element number from the
PDF reader through to the manifest and adds a two-way numbering menu.

**Tech Stack:** C# / .NET 10, xUnit + FluentAssertions, Avalonia 12.1.1 (headless for UI tests),
CommunityToolkit.Mvvm.

**Spec:** `docs/superpowers/specs/2026-08-29-catalogue-actions-design.md`

## Global Constraints

- Target frameworks are multi-targeted; build with `dotnet build Lego2STL.slnx -c Debug`.
- Tests: `dotnet test Lego2STL.slnx`. Two projects: `Lego2STL.Tests` (core) and
  `Lego2STL.UiTests` (Avalonia headless, uses `[AvaloniaFact]`).
- Every user-facing string goes through `TextKey` and is added to **both**
  `Strings.English.cs` and `Strings.Italian.cs`. `StringsTests` fails the build if a key is
  missing from either.
- Code comments and CHANGELOG entries: **one sentence each**. Test comments are exempt.
- Commit messages: `<type>: <description>`, describing observable behaviour, never internal
  class or method names.
- Files stay under 800 lines; functions under 50.
- The parts-list CSV keeps its six columns. Do not add a seventh.
- Weld tolerances are in **LDraw units where 1 unit = 0.4 mm**, not millimetres.
- Escalation ladder, fixed: `5e-3f, 2e-2f, 5e-2f, 1e-1f`.

---

### Task 1: The fill stops manufacturing overused edges

**Files:**
- Modify: `src/Lego2STL.Core/Geometry/BoundaryFill.cs:134-186` (the `Loops` method)
- Test: `tests/Lego2STL.Tests/Geometry/BoundaryFillTests.cs`

**Interfaces:**
- Consumes: nothing from earlier tasks.
- Produces: no signature change. `BoundaryFill.Fill(IndexedMesh)` still returns
  `BoundaryFillResult(IndexedMesh Mesh, int LoopsFilled, int TrianglesAdded, int LoopsLeftOpen)`.
  What changes is that `LoopsFilled` can now be larger for the same input, and the filled mesh
  has no overused edges where it previously had some.

- [ ] **Step 1: Write the failing test**

Add to `tests/Lego2STL.Tests/Geometry/BoundaryFillTests.cs`:

```csharp
    /// <summary>
    /// Two gaps that meet at a single corner are covered separately.
    /// </summary>
    /// <remarks>
    /// Two triangles sharing exactly one corner is the smallest shape whose free edges walk
    /// into one path that passes through the same vertex twice. Covered as a single loop, the
    /// fan reuses the edge from its centre to that vertex four times over, and the result is a
    /// mesh with no holes that still does not count as closed. Measured on run 6324712, this
    /// alone accounted for 19 of the 52 parts reported as unrepaired.
    /// </remarks>
    [Fact]
    public void Two_gaps_meeting_at_a_corner_are_covered_as_two()
    {
        var vertices = new List<Vector3>
        {
            new(0, 0, 0), new(1, 0, 0), new(2, 0, 0), new(0, 1, 0), new(2, 1, 0),
        };

        // Sharing vertex 1, numbered so the walk starts at 0 and reaches 1 twice.
        var mesh = new IndexedMesh(vertices, [new IndexedTriangle(0, 1, 3), new IndexedTriangle(1, 2, 4)]);

        MeshAnalysis.Measure(mesh).OverusedEdgeCount.Should().Be(0, "the source has none");

        var filled = BoundaryFill.Fill(mesh);
        var quality = MeshAnalysis.Measure(filled.Mesh);

        filled.LoopsFilled.Should().Be(2, "the two gaps are separate gaps");
        quality.OpenEdgeCount.Should().Be(0);
        quality.OverusedEdgeCount.Should().Be(0, "the fill must not invent what it then reports");
        quality.IsClosed.Should().BeTrue();
    }
```

- [ ] **Step 2: Run it and watch it fail**

```
dotnet test tests/Lego2STL.Tests/Lego2STL.Tests.csproj --filter "FullyQualifiedName~Two_gaps_meeting_at_a_corner"
```

Expected: FAIL. Measured before writing this plan: `LoopsFilled` is 1, `OverusedEdgeCount` is 1,
`IsClosed` is false.

- [ ] **Step 3: Detach a ring when the walk returns to a vertex already on the path**

Replace the body of `Loops` in `src/Lego2STL.Core/Geometry/BoundaryFill.cs`. The `<remarks>`
above it should be updated to describe the new behaviour:

```csharp
    /// <summary>
    /// Walks the free edges into closed loops, consuming each edge once.
    /// </summary>
    /// <remarks>
    /// A path that arrives back at a vertex it has already been through is not one gap but two
    /// meeting at a point. The ring that closes there is detached and covered on its own,
    /// because a single fan across both would use the edge from its centre to that vertex four
    /// times over - leaving a shape with no holes that still does not count as closed.
    /// Where a corner has several free edges leaving it, the lowest-numbered is taken, so that
    /// the same surface always produces the same loops and therefore the same file.
    /// </remarks>
    private static List<List<int>> Loops(Dictionary<int, List<int>> next, ref int leftOpen)
    {
        foreach (var targets in next.Values)
        {
            targets.Sort();
        }

        var loops = new List<List<int>>();
        var starts = next.Keys.Order().ToList();

        foreach (var start in starts)
        {
            while (next.TryGetValue(start, out var fromStart) && fromStart.Count > 0)
            {
                var loop = new List<int> { start };
                var where = new Dictionary<int, int> { [start] = 0 };
                var current = Take(next, start);

                var closed = false;

                while (loop.Count < MaxLoopLength)
                {
                    if (current == start)
                    {
                        closed = true;
                        break;
                    }

                    if (where.TryGetValue(current, out var earlier))
                    {
                        loops.Add(loop[earlier..]);

                        for (var i = earlier; i < loop.Count; i++)
                        {
                            where.Remove(loop[i]);
                        }

                        loop.RemoveRange(earlier, loop.Count - earlier);
                    }

                    where[current] = loop.Count;
                    loop.Add(current);

                    if (!next.TryGetValue(current, out var onward) || onward.Count == 0)
                    {
                        break;
                    }

                    current = Take(next, current);
                }

                if (closed)
                {
                    loops.Add(loop);
                }
                else
                {
                    // A chain that never came back: the surface branches here, and guessing a
                    // cover would invent the wrong thing. Counted and left alone.
                    leftOpen++;
                }
            }
        }

        return loops;
    }
```

- [ ] **Step 4: Run the test and the whole geometry suite**

```
dotnet test tests/Lego2STL.Tests/Lego2STL.Tests.csproj --filter "FullyQualifiedName~Geometry"
```

Expected: PASS, including the existing `BoundaryFillTests`, `ClearanceOffsetTests` and
`TJunctionRepairTests` — the clearance offset needs a closed shape, so it exercises this path.

- [ ] **Step 5: Commit**

```bash
git add src/Lego2STL.Core/Geometry/BoundaryFill.cs tests/Lego2STL.Tests/Geometry/BoundaryFillTests.cs
git commit -m "fix: covering two gaps that touch no longer leaves a shape unclosed"
```

---

### Task 2: A more tolerant weld, only where it is needed

**Files:**
- Modify: `src/Lego2STL.Core/Geometry/MeshPipeline.cs` (the `Prepare` method and `PreparedMesh`)
- Test: `tests/Lego2STL.Tests/Geometry/MeshPipelineTests.cs` (create)

**Interfaces:**
- Consumes: `BoundaryFill.Fill` as corrected in Task 1.
- Produces: `PreparedMesh` gains a trailing member `float? ClosedAtTolerance = null` — the
  tolerance that closed the shape when it took more than the one asked for, and null when it
  did not. Task 3 records it.

- [ ] **Step 1: Write the failing tests**

Create `tests/Lego2STL.Tests/Geometry/MeshPipelineTests.cs`:

```csharp
using System.Numerics;
using FluentAssertions;
using Lego2STL.Core.Geometry;
using Lego2STL.Core.LDraw;
using Xunit;

namespace Lego2STL.Tests.Geometry;

/// <summary>
/// Preparing a part's surfaces, and what it is allowed to do to get them closed.
/// </summary>
/// <remarks>
/// The escalation exists because welding at the tolerance asked for leaves some parts open
/// while a slightly larger one closes them exactly - measured across run 6324712, 8 of the 33
/// parts with real holes. It is bounded well below what a nozzle can resolve, and it must never
/// touch a shape that was already closed, which is what the second test here is for.
/// </remarks>
public sealed class MeshPipelineTests
{
    /// <summary>A shape already closed is not re-prepared, so it cannot change at all.</summary>
    [Fact]
    public void A_shape_that_is_already_closed_is_left_exactly_as_it_was()
    {
        var part = ABox();

        var prepared = MeshPipeline.Prepare(part);

        prepared.Quality.IsClosed.Should().BeTrue("a box is closed to begin with");
        prepared.ClosedAtTolerance.Should().BeNull("nothing had to be escalated");
        prepared.Mesh.TriangleCount.Should().Be(part.Triangles.Count);
    }

    /// <summary>A gap too wide for the asked-for tolerance is closed by a larger one.</summary>
    [Fact]
    public void A_shape_still_open_is_tried_again_more_tolerantly()
    {
        var part = ABoxWithOneCornerNudged(0.01f);

        var prepared = MeshPipeline.Prepare(part, new MeshPipelineOptions
        {
            WeldTolerance = 1e-4f,
        });

        prepared.Quality.IsClosed.Should().BeTrue();
        prepared.ClosedAtTolerance.Should().NotBeNull("it took more than was asked for");
        prepared.ClosedAtTolerance.Should().BeLessThanOrEqualTo(0.1f, "the ladder is bounded");
    }

    /// <summary>Turning repair off turns the escalation off with it.</summary>
    [Fact]
    public void Asking_for_no_repair_asks_for_no_escalation_either()
    {
        var part = ABoxWithOneCornerNudged(0.01f);

        var prepared = MeshPipeline.Prepare(part, new MeshPipelineOptions
        {
            WeldTolerance = 1e-4f,
            FillGaps = false,
        });

        prepared.ClosedAtTolerance.Should().BeNull();
    }

    /// <summary>A closed box: eight corners, twelve triangles, every edge shared twice.</summary>
    private static PartMesh ABox() => new(
        Reference: "box",
        Title: "a box",
        Triangles: [.. BoxTriangles(Vector3.Zero)],
        MovedTo: null,
        FilesUsed: 1,
        MissingReferences: []);

    /// <summary>
    /// The same box with one face's corner moved, so that face no longer meets its neighbours
    /// within a tight tolerance but does within a looser one.
    /// </summary>
    private static PartMesh ABoxWithOneCornerNudged(float by)
    {
        var triangles = BoxTriangles(Vector3.Zero).ToList();

        for (var i = 0; i < triangles.Count; i++)
        {
            var t = triangles[i];
            triangles[i] = new Triangle(Nudge(t.A), Nudge(t.B), Nudge(t.C));
        }

        return new PartMesh("nudged", "a box with a gap", triangles, null, 1, []);

        Vector3 Nudge(Vector3 v) =>
            v == new Vector3(1, 1, 1) ? new Vector3(1 + by, 1, 1) : v;
    }

    private static IEnumerable<Triangle> BoxTriangles(Vector3 origin)
    {
        Vector3[] c =
        [
            origin + new Vector3(0, 0, 0), origin + new Vector3(1, 0, 0),
            origin + new Vector3(1, 1, 0), origin + new Vector3(0, 1, 0),
            origin + new Vector3(0, 0, 1), origin + new Vector3(1, 0, 1),
            origin + new Vector3(1, 1, 1), origin + new Vector3(0, 1, 1),
        ];

        int[][] faces =
        [
            [0, 3, 2, 1], [4, 5, 6, 7], [0, 1, 5, 4],
            [1, 2, 6, 5], [2, 3, 7, 6], [3, 0, 4, 7],
        ];

        foreach (var f in faces)
        {
            yield return new Triangle(c[f[0]], c[f[1]], c[f[2]]);
            yield return new Triangle(c[f[0]], c[f[2]], c[f[3]]);
        }
    }
}
```

- [ ] **Step 2: Run and watch them fail**

```
dotnet test tests/Lego2STL.Tests/Lego2STL.Tests.csproj --filter "FullyQualifiedName~MeshPipelineTests"
```

Expected: FAIL to **compile** — `PreparedMesh` has no `ClosedAtTolerance`. That is the correct
first failure.

- [ ] **Step 3: Add the member to `PreparedMesh`**

In `src/Lego2STL.Core/Geometry/MeshPipeline.cs`, append to the record's parameter list, after
`ClearanceRefusedBecause`:

```csharp
    bool ClearanceApplied = false,
    string? ClearanceRefusedBecause = null,
    float? ClosedAtTolerance = null)
```

- [ ] **Step 4: Split `Prepare` into one attempt plus the escalation**

Replace `Prepare` in `src/Lego2STL.Core/Geometry/MeshPipeline.cs`:

```csharp
    /// <summary>
    /// Tolerances to try when the one asked for leaves the shape open, smallest first.
    /// </summary>
    /// <remarks>
    /// In source units, where one unit is 0.4 mm, so the largest is 0.04 mm - below what a
    /// 0.4 mm nozzle can lay down, and therefore too small to deform anything it closes.
    /// </remarks>
    private static readonly float[] HarderTolerances = [5e-3f, 2e-2f, 5e-2f, 1e-1f];

    public static PreparedMesh Prepare(PartMesh part, MeshPipelineOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(part);

        var o = options ?? new MeshPipelineOptions();
        var prepared = Attempt(part, o, o.WeldTolerance);

        if (!o.FillGaps || prepared.Quality.IsClosed)
        {
            return prepared;
        }

        // Each attempt starts from the source triangles rather than from the last result:
        // re-welding a welded mesh compounds the tolerance instead of applying it.
        foreach (var tolerance in HarderTolerances)
        {
            if (tolerance <= o.WeldTolerance)
            {
                continue;
            }

            var harder = Attempt(part, o, tolerance);

            if (harder.Quality.IsClosed)
            {
                return harder with { ClosedAtTolerance = tolerance };
            }
        }

        return prepared;
    }

    private static PreparedMesh Attempt(PartMesh part, MeshPipelineOptions o, float tolerance)
    {
        var welded = VertexWelder.Weld(part.Triangles, tolerance);
        var tidied = welded.WithoutDegenerateTriangles(out var degenerateRemoved);

        var before = MeshAnalysis.Measure(tidied);

        var seamsClosed = 0;
        IndexedMesh repaired = o.RepairSeams
            ? TJunctionRepair.Repair(tidied, out seamsClosed, tolerance)
            : tidied;

        var gapsFilled = 0;
        if (o.FillGaps)
        {
            var covered = BoundaryFill.Fill(repaired);
            repaired = covered.Mesh;
            gapsFilled = covered.LoopsFilled;
        }

        var quality = MeshAnalysis.Measure(repaired);

        // Millimetres first, because a clearance is stated in millimetres and has to be applied
        // to a shape that is already measured in them.
        var upright = StandUp(repaired, o);
        var clearance = ClearanceOffset.Apply(upright, o.ClearanceMillimetres, quality);
        var placed = SitOnBed(clearance.Mesh, o);

        return new PreparedMesh(
            part.Reference,
            part.Title,
            placed,
            quality,
            before,
            seamsClosed,
            gapsFilled,
            degenerateRemoved,
            part.MovedTo,
            part.MissingReferences,
            clearance.Applied,
            clearance.Reason);
    }
```

- [ ] **Step 5: Run the tests**

```
dotnet test tests/Lego2STL.Tests/Lego2STL.Tests.csproj --filter "FullyQualifiedName~Geometry"
```

Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add src/Lego2STL.Core/Geometry/MeshPipeline.cs tests/Lego2STL.Tests/Geometry/MeshPipelineTests.cs
git commit -m "fix: a shape left open is prepared again more tolerantly before giving up"
```

---

### Task 3: The record carries both counts, and the catalogue says which

**Files:**
- Modify: `src/Lego2STL.Core/Run/RunManifest.cs` (`ManifestPart`, the `Part` factory)
- Modify: `src/Lego2STL.Core/Run/RunDocument.cs` (`RunDocumentPart`, the projection)
- Modify: `src/Lego2STL.Core/Text/TextKey.cs`, `Strings.English.cs`, `Strings.Italian.cs`
- Modify: `src/Lego2STL.Gui/ViewModels/CataloguePartViewModel.cs`
- Test: `tests/Lego2STL.Tests/Run/RunManifestTests.cs`, `tests/Lego2STL.UiTests/CatalogueTests.cs`

**Interfaces:**
- Consumes: `PreparedMesh.ClosedAtTolerance` from Task 2; `MeshQuality.OverusedEdgeCount`, which
  already exists and is already measured.
- Produces: `RunDocumentPart.OverusedEdgeCount` (`int?`) and `.ClosedAtTolerance` (`float?`);
  `CataloguePartViewModel.HasSelfIntersection` (`bool`).

- [ ] **Step 1: Write the failing tests**

Add to `tests/Lego2STL.Tests/Run/RunManifestTests.cs`:

```csharp
    /// <summary>
    /// Both ways a shape can fail to be closed are recorded, because they are different faults.
    /// </summary>
    /// <remarks>
    /// Only the open-edge count used to be kept, so a part with no holes whose surfaces pass
    /// through each other was recorded as indistinguishable from a part full of holes - and the
    /// catalogue told 19 parts of run 6324712 they had open edges when they had none.
    /// </remarks>
    [Fact]
    public void A_shape_records_both_kinds_of_fault()
    {
        var layout = ARunFolder();

        var manifest = RunManifest.From(
            APretendRun.Complete(layout), APretendRun.Started, APretendRun.Finished, null);

        var measured = manifest.Parts.Where(part => part.IsClosed is not null).ToList();

        measured.Should().NotBeEmpty();
        measured.Should().OnlyContain(part => part.OverusedEdgeCount is not null,
            "a shape that was measured was measured for both");
    }
```

Add to `tests/Lego2STL.UiTests/CatalogueTests.cs`:

```csharp
    /// <summary>Surfaces passing through each other is not the same fault as holes.</summary>
    [AvaloniaFact]
    public void A_shape_with_no_holes_is_not_told_it_has_open_edges()
    {
        var part = new RunDocumentPart(
            1, "32064a", 11, "Black", Rgb24.Parse("#05131D"), 2,
            Title: "a part", Size: "32 x 16 x 22.4 mm",
            IsClosed: false, OpenEdgeCount: 0, ThinnestSpanMm: 8,
            OverusedEdgeCount: 2, ClosedAtTolerance: null);

        var card = new CataloguePartViewModel(part, null, null);

        card.HasOpenEdges.Should().BeFalse();
        card.HasSelfIntersection.Should().BeTrue();
        card.HasWarning.Should().BeTrue();
        card.WarningText.Should().NotContain(
            Loc.Current.Text(TextKey.UiWarningNotClosed),
            "it has no open edges to warn about");
    }
```

- [ ] **Step 2: Run and watch them fail**

```
dotnet test Lego2STL.slnx --filter "FullyQualifiedName~A_shape_records_both_kinds|FullyQualifiedName~A_shape_with_no_holes"
```

Expected: FAIL to compile — neither member exists.

- [ ] **Step 3: Add the two fields to the record and the projection**

In `src/Lego2STL.Core/Run/RunManifest.cs`, extend `ManifestPart`:

```csharp
public sealed record ManifestPart(
    int Id,
    string Part,
    int ColorCode,
    string Color,
    string Rgb,
    int Quantity,
    string? Title,
    string? Size,
    bool? IsClosed,
    int? OpenEdgeCount,
    double? ThinnestSpanMm,
    int? OverusedEdgeCount = null,
    float? ClosedAtTolerance = null);
```

In the same file, the `Part` factory already reads `shape`; append the two arguments to the
`new ManifestPart(...)` call it returns:

```csharp
            shape?.Quality.OverusedEdgeCount,
            shape?.ClosedAtTolerance);
```

In `src/Lego2STL.Core/Run/RunDocument.cs`, extend `RunDocumentPart` the same way:

```csharp
    int? OpenEdgeCount,
    double? ThinnestSpanMm,
    int? OverusedEdgeCount = null,
    float? ClosedAtTolerance = null)
```

and add to the projection in `RunDocument.From`, after `part.ThinnestSpanMm`:

```csharp
                    part.OverusedEdgeCount,
                    part.ClosedAtTolerance)),
```

Then fix `RunDocumentPart`'s body. **`HasOpenEdges` is currently `IsClosed == false`, which is
the actual source of the wrong message** — it calls every unclosed shape holed, whatever the
fault. Replace it and add the second fault beside it:

```csharp
    /// <summary>Holes in the surface. Not merely "not closed" - that is two faults, not one.</summary>
    public bool HasOpenEdges => OpenEdgeCount > 0;

    /// <summary>
    /// Surfaces that pass through each other, which is not the same as a hole.
    /// </summary>
    /// <remarks>
    /// A run recorded before both counts were kept has no overused figure, but it does say the
    /// shape was not closed and had no open edges, and only this fault can produce that pair -
    /// so runs already on disk name the right fault without being made again.
    /// </remarks>
    public bool HasSelfIntersection =>
        OverusedEdgeCount > 0
        || (OverusedEdgeCount is null && IsClosed == false && OpenEdgeCount == 0);
```

- [ ] **Step 4: Add the wording**

In `src/Lego2STL.Core/Text/TextKey.cs`, beside `UiWarningNotClosed`:

```csharp
    /// <summary>Said of a shape whose surfaces pass through each other rather than gape.</summary>
    UiWarningSelfIntersects,
```

In `Strings.English.cs`, beside `UiWarningNotClosed`:

```csharp
            [TextKey.UiWarningSelfIntersects] =
                "Some of this shape's surfaces pass through each other; a slicer will still print it.",
```

In `Strings.Italian.cs`:

```csharp
            [TextKey.UiWarningSelfIntersects] =
                "Alcune superfici di questa forma si compenetrano; lo slicer la stampa comunque.",
```

- [ ] **Step 5: Let the card tell them apart**

In `src/Lego2STL.Gui/ViewModels/CataloguePartViewModel.cs`, add beside `HasOpenEdges`:

```csharp
    /// <summary>True when the shape has no holes but its surfaces pass through each other.</summary>
    public bool HasSelfIntersection => Part.HasSelfIntersection;
```

and extend `Warnings`, after the `HasOpenEdges` block:

```csharp
            if (HasSelfIntersection)
            {
                warnings.Add(Localization.Loc.Current.Text(Core.Text.TextKey.UiWarningSelfIntersects));
            }
```

`HasWarning` comes from `RunDocumentPart.HasWarning`. Find it in `RunDocument.cs` and include
the new fault, so a part with only this fault still shows its band:

```csharp
    public bool HasWarning => HasOpenEdges || HasThinFeatures || HasSelfIntersection;
```

Finally, add `HasSelfIntersection` to the list of names re-raised in
`RunDocumentViewModel.Reword`, beside `WarningText`, so switching language re-words it.

- [ ] **Step 6: Run the full suite**

```
dotnet test Lego2STL.slnx
```

Expected: PASS. `StringsTests` proves both languages carry the new key.

- [ ] **Step 7: Commit**

```bash
git add -A
git commit -m "feat: the catalogue tells a shape with holes from one whose surfaces overlap"
```

---

### Task 4: Measure the result on the real run and report it

**Files:**
- Modify: `docs/superpowers/specs/2026-08-29-catalogue-actions-design.md` (§1.4 only)

This task produces no code. It closes the loop on the number the spec projects.

- [ ] **Step 1: Re-run the reference set**

```bash
dotnet build src/Lego2STL.Cli/Lego2STL.Cli.csproj -c Debug
./src/Lego2STL.Cli/bin/Debug/net10.0-windows10.0.19041.0/lego2stl.exe build 6324712/6324712.csv --scale 200 --lang it --output-dir /tmp/lotb
```

- [ ] **Step 2: Count the closed shapes**

```bash
python -c "
import json; d=json.load(open('/tmp/lotb/6324712/run.json',encoding='utf-8-sig'))
s={p['part']:p for p in d['parts'] if p.get('isClosed') is not None}
print(sum(1 for p in s.values() if p['isClosed']), 'of', len(s))
"
```

- [ ] **Step 3: Record the measured figure in §1.4 of the spec**

Replace the projection with the number measured, whatever it is. If it is materially below the
projected ~150, stop and report rather than adjusting the ladder to chase the number — the
ladder's ceiling is a safety property, not a tuning knob.

- [ ] **Step 4: Commit**

```bash
git add docs/superpowers/specs/2026-08-29-catalogue-actions-design.md
git commit -m "docs: record what the repair change actually achieved"
```

---

### Task 5: Oversized parts become data

**Files:**
- Modify: `src/Lego2STL.Core/Plates/PlateBuilder.cs` (`PlateBuildResult`, `Describe`, `WriteAsync`)
- Modify: `src/Lego2STL.Core/Pipeline/RunReport.cs:289-296`
- Test: `tests/Lego2STL.Tests/Plates/PlateBuilderTests.cs`

**Interfaces:**
- Consumes: `OversizedItem(PackableItem Item, bool TooTall)` and
  `PackableItem(string PartNumber, Vector2 Footprint, float Height)`, both unchanged.
- Produces: `SkippedPart(string PartNumber, float Width, float Depth, float Height, bool TooTall)`
  and `PlateBuildResult.Skipped` retyped to `IReadOnlyList<SkippedPart>`. Task 6 reads it;
  Task 7 shows it.

- [ ] **Step 1: Write the failing test**

Add to `tests/Lego2STL.Tests/Plates/PlateBuilderTests.cs`:

```csharp
    /// <summary>
    /// A part no plate can take is recorded with its measurements, not just described.
    /// </summary>
    /// <remarks>
    /// It used to be kept as a finished sentence, which is why nothing but the report could say
    /// anything about it - the catalogue could not offer a smaller scale because it did not
    /// know by how much the part missed.
    /// </remarks>
    [Fact]
    public async Task A_part_too_big_for_the_bed_is_recorded_with_its_size()
    {
        var folder = APretendRun.TempFolder("plates");

        var list = new PartsList(
            [new PartEntry(1, "huge", 11, "Black", Rgb24.Parse("#05131D"), 1)], []);

        // 400 mm across, against a bed 256 mm wide.
        var shapes = new Dictionary<string, IndexedMesh>(StringComparer.OrdinalIgnoreCase)
        {
            ["huge"] = ABoxOf(400f, 10f, 10f),
        };

        var built = await PlateBuilder.WriteAsync(list, shapes, folder);

        var skipped = built.Skipped.Should().ContainSingle().Subject;

        skipped.PartNumber.Should().Be("huge");
        skipped.Width.Should().BeApproximately(400f, 0.1f);
        skipped.TooTall.Should().BeFalse("it is wide, not tall");
    }

    private static IndexedMesh ABoxOf(float x, float y, float z) =>
        VertexWelder.Weld(
        [
            new Triangle(new Vector3(0, 0, 0), new Vector3(x, 0, 0), new Vector3(x, y, 0)),
            new Triangle(new Vector3(0, 0, 0), new Vector3(x, y, 0), new Vector3(0, y, 0)),
            new Triangle(new Vector3(0, 0, z), new Vector3(x, y, z), new Vector3(x, 0, z)),
            new Triangle(new Vector3(0, 0, z), new Vector3(0, y, z), new Vector3(x, y, z)),
        ]);
```

- [ ] **Step 2: Run and watch it fail**

```
dotnet test tests/Lego2STL.Tests/Lego2STL.Tests.csproj --filter "FullyQualifiedName~A_part_too_big_for_the_bed"
```

Expected: FAIL to compile — `Skipped` is `IReadOnlyList<string>`.

- [ ] **Step 3: Introduce the record and retype the result**

In `src/Lego2STL.Core/Plates/PlateBuilder.cs`, above `PlateBuildResult`:

```csharp
/// <summary>A part no plate could take, with the measurements that ruled it out.</summary>
/// <param name="TooTall">
/// Whether it was the height rather than the footprint. Kept apart because a taller bed and a
/// smaller scale are different answers.
/// </param>
public sealed record SkippedPart(
    string PartNumber,
    float Width,
    float Depth,
    float Height,
    bool TooTall);
```

Change `PlateBuildResult`:

```csharp
public sealed record PlateBuildResult(
    IReadOnlyList<BuiltPlate> Plates,
    IReadOnlyList<SkippedPart> Skipped)
```

In `WriteAsync`, change the declaration and what is added to it:

```csharp
        var skipped = new List<SkippedPart>();
```

```csharp
            foreach (var over in packed.Oversized.DistinctBy(x => x.Item.PartNumber))
            {
                skipped.Add(new SkippedPart(
                    over.Item.PartNumber,
                    over.Item.Footprint.X,
                    over.Item.Footprint.Y,
                    over.Item.Height,
                    over.TooTall));
            }
```

Replace `Describe` — it now formats a `SkippedPart` and is used by the report rather than here:

```csharp
    /// <summary>Why a part is not on any plate, said the way the report prints it.</summary>
    public static string Describe(SkippedPart part, Strings words, PrintBed bed)
    {
        ArgumentNullException.ThrowIfNull(part);
        ArgumentNullException.ThrowIfNull(words);
        ArgumentNullException.ThrowIfNull(bed);

        return part.TooTall
            ? words.Format(
                TextKey.ErrPartTooTallForBed,
                part.PartNumber,
                part.Height.ToString("0.#", CultureInfo.InvariantCulture),
                bed.Height.ToString("0.#", CultureInfo.InvariantCulture))
            : words.Format(
                TextKey.ErrPlateTooSmall,
                part.PartNumber,
                string.Create(CultureInfo.InvariantCulture, $"{part.Width:0.#} x {part.Depth:0.#} mm"),
                bed.Name);
    }
```

- [ ] **Step 4: Let the report format them**

In `src/Lego2STL.Core/Pipeline/RunReport.cs`, replace the loop at the `plates.Skipped` block.
The surrounding method already has `words`; the bed comes from the run's settings, which the
method reaches as `outcome.Settings`:

```csharp
        if (plates.Skipped.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine(words[TextKey.ReportPlateDidNotFit]);

            var bed = PrintBeds.TryGetByName(outcome.Settings.Printer, out var named)
                ? named
                : PrintBeds.Default;

            foreach (var part in plates.Skipped)
            {
                sb.AppendLine("  " + PlateBuilder.Describe(part, words, bed));
            }
        }
```

The enclosing method is `Plates(StringBuilder sb, Strings words, RunOutcome outcome)` at
`RunReport.cs:249`, so `outcome` is already in scope and nothing needs passing in.

Note while you are in there, and **leave alone**: that method returns early on
`outcome.Plates is not { Plates.Count: > 0 }`, so a run where *every* part was too big prints no
"did not fit" list at all. It is a real defect and it is out of this plan's scope — the
catalogue reads `DidNotFit` from the manifest, not from the report, so Task 7 is unaffected.
Raise it separately rather than widening this task.

- [ ] **Step 5: Run the plate tests, then the whole suite**

```
dotnet test Lego2STL.slnx
```

Expected: PASS. The report's wording is unchanged, so any snapshot over `report.txt` still
matches — if one does not, the formatting drifted and must be corrected here, not re-baselined.

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "refactor: a part left off the plates is recorded with its measurements"
```

---

### Task 6: The largest scale that would fit

**Files:**
- Create: `src/Lego2STL.Core/Plates/FittingScale.cs`
- Modify: `src/Lego2STL.Core/Run/RunManifest.cs`, `src/Lego2STL.Core/Run/RunDocument.cs`
- Test: `tests/Lego2STL.Tests/Plates/FittingScaleTests.cs`

**Interfaces:**
- Consumes: `SkippedPart` from Task 5; `PackingOptions.Bed` and `.Margin`, both existing.
- Produces: `FittingScale.Largest(IEnumerable<PackableItem>, PrintBed, float margin, double scaleUsed)`
  returning `double?` — the largest whole percent at which every part fits, or null when they
  already do. `RunManifest.LargestFittingScalePercent` and
  `RunDocument.LargestFittingScalePercent`, both `double?`. Task 7 shows it.

- [ ] **Step 1: Write the failing test**

Create `tests/Lego2STL.Tests/Plates/FittingScaleTests.cs`:

```csharp
using System.Numerics;
using FluentAssertions;
using Lego2STL.Core.Plates;
using Xunit;

namespace Lego2STL.Tests.Plates;

/// <summary>
/// The largest scale at which everything still fits the plate.
/// </summary>
/// <remarks>
/// The point of the number is that acting on it works, so the tests check that the parts fit
/// when it is applied rather than comparing against a figure written down here. A suggestion
/// the packer would then reject is worse than no suggestion.
/// </remarks>
public sealed class FittingScaleTests
{
    private static readonly PrintBed A1 = PrintBeds.A1;

    [Fact]
    public void Nothing_is_suggested_when_everything_already_fits()
    {
        var items = new[] { new PackableItem("small", new Vector2(40, 40), 20) };

        FittingScale.Largest(items, A1, margin: 5f, scaleUsed: 100).Should().BeNull();
    }

    /// <summary>The measured case: a part 304 mm across at 200%, on a 256 mm bed.</summary>
    [Fact]
    public void A_part_wider_than_the_bed_brings_the_whole_set_down()
    {
        var items = new[]
        {
            new PackableItem("46891", new Vector2(304f, 184.8f), 192.2f),
            new PackableItem("small", new Vector2(40, 40), 20),
        };

        var suggested = FittingScale.Largest(items, A1, margin: 5f, scaleUsed: 200);

        suggested.Should().NotBeNull();
        suggested.Should().BeLessThan(200);

        // What matters is that applying it works.
        var factor = (float)(suggested!.Value / 200);
        var shrunk = items.Select(i => i with
        {
            Footprint = i.Footprint * factor,
            Height = i.Height * factor,
        });

        ShelfPacker.Pack(shrunk.ToList(), new PackingOptions { Bed = A1, Margin = 5f })
            .Oversized.Should().BeEmpty("the suggestion has to be one the packer accepts");
    }

    [Fact]
    public void A_part_too_tall_counts_as_much_as_one_too_wide()
    {
        var items = new[] { new PackableItem("tower", new Vector2(20, 20), 500f) };

        var suggested = FittingScale.Largest(items, A1, margin: 5f, scaleUsed: 100);

        suggested.Should().NotBeNull().And.BeLessThan(100);
    }

    /// <summary>Rounded down, because a suggestion that overshoots is a suggestion that fails.</summary>
    [Fact]
    public void The_answer_is_a_whole_percent_and_never_rounds_up()
    {
        var items = new[] { new PackableItem("odd", new Vector2(333.3f, 20), 20) };

        var suggested = FittingScale.Largest(items, A1, margin: 5f, scaleUsed: 100);

        suggested.Should().Be(Math.Floor(suggested!.Value));
    }
}
```

- [ ] **Step 2: Run and watch it fail**

```
dotnet test tests/Lego2STL.Tests/Lego2STL.Tests.csproj --filter "FullyQualifiedName~FittingScaleTests"
```

Expected: FAIL to compile — `FittingScale` does not exist.

- [ ] **Step 3: Write it**

Create `src/Lego2STL.Core/Plates/FittingScale.cs`:

```csharp
namespace Lego2STL.Core.Plates;

/// <summary>
/// The largest scale at which every part still fits the plate.
/// </summary>
/// <remarks>
/// Measured against the same usable area the packer measures against - the bed less its margin
/// on both sides - because a scale the packer would then reject is worse than no suggestion at
/// all. Rounded down for the same reason.
/// </remarks>
public static class FittingScale
{
    /// <param name="scaleUsed">The percentage the run was made at, which the items are already at.</param>
    /// <returns>The largest whole percent that fits, or null when everything already fits.</returns>
    public static double? Largest(
        IEnumerable<PackableItem> items,
        PrintBed bed,
        float margin,
        double scaleUsed)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(bed);

        var usableWidth = bed.Width - (2f * margin);
        var usableDepth = bed.Depth - (2f * margin);

        var worst = double.MaxValue;
        var any = false;

        foreach (var item in items)
        {
            any = true;

            var factor = Math.Min(
                Math.Min(Room(usableWidth, item.Footprint.X), Room(usableDepth, item.Footprint.Y)),
                Room(bed.Height, item.Height));

            worst = Math.Min(worst, factor);
        }

        if (!any || worst >= 1)
        {
            return null;
        }

        var suggested = Math.Floor(scaleUsed * worst);
        return suggested < 1 ? 1 : suggested;
    }

    /// <summary>How much of the room a measurement leaves; more than one means it fits.</summary>
    private static double Room(float available, float needed) =>
        needed <= 0 ? double.MaxValue : available / needed;
}
```

- [ ] **Step 4: Run the test**

```
dotnet test tests/Lego2STL.Tests/Lego2STL.Tests.csproj --filter "FullyQualifiedName~FittingScaleTests"
```

Expected: PASS.

- [ ] **Step 5: Record it on the run**

In `src/Lego2STL.Core/Run/RunManifest.cs`, beside `PlateCount`:

```csharp
    /// <summary>The largest scale at which every part would fit, when some did not.</summary>
    public double? LargestFittingScalePercent { get; init; }
```

In `RunManifest.From`, the plate stage already has the built plates. Compute from the same
shapes the builder saw — `outcome.ShapesByPart` — and the run's own bed and margin:

```csharp
            LargestFittingScalePercent = FittingScale.Largest(
                outcome.ShapesByPart.Select(pair =>
                {
                    var (min, max) = pair.Value.Bounds();
                    var size = max - min;
                    return new PackableItem(pair.Key, new Vector2(size.X, size.Y), size.Z);
                }),
                PrintBeds.TryGetByName(outcome.Settings.Printer, out var bed) ? bed : PrintBeds.Default,
                margin: new PackingOptions().Margin,
                scaleUsed: outcome.Settings.ScalePercent),
```

Mirror the property on `RunDocument` and carry it in `RunDocument.From`:

```csharp
    public double? LargestFittingScalePercent { get; init; }
```

```csharp
            LargestFittingScalePercent = manifest.LargestFittingScalePercent,
```

- [ ] **Step 6: Run the whole suite and commit**

```
dotnet test Lego2STL.slnx
```

```bash
git add -A
git commit -m "feat: a run works out the largest scale at which every part would fit"
```

---

### Task 7: The catalogue offers the way out

**Files:**
- Modify: `src/Lego2STL.Core/Run/RunManifest.cs`, `src/Lego2STL.Core/Run/RunDocument.cs`
  (carry `Skipped` through)
- Modify: `src/Lego2STL.Gui/ViewModels/RunDocumentViewModel.cs`,
  `src/Lego2STL.Gui/ViewModels/CataloguePartViewModel.cs`,
  `src/Lego2STL.Gui/ViewModels/RunCatalogue.cs`
- Modify: `src/Lego2STL.Gui/Views/CatalogueView.axaml`
- Modify: `src/Lego2STL.Core/Text/TextKey.cs`, `Strings.English.cs`, `Strings.Italian.cs`
- Test: `tests/Lego2STL.UiTests/CatalogueTests.cs`

**Interfaces:**
- Consumes: `SkippedPart` (Task 5), `RunDocument.LargestFittingScalePercent` (Task 6).
- Produces: `RunDocumentViewModel.HasPartsThatDoNotFit` (`bool`),
  `.DoesNotFitText` (`string`), `.TryASmallerScaleCommand`;
  `CataloguePartViewModel.DoesNotFitThePlate` (`bool`).

- [ ] **Step 1: Write the failing test**

Add to `tests/Lego2STL.UiTests/CatalogueTests.cs`:

```csharp
    /// <summary>
    /// A run with a part too big says so, and offers the scale that would fit.
    /// </summary>
    /// <remarks>
    /// Pressing the offer starts again from this run's own parts list, so it lands in the same
    /// folder rather than scattering a second copy - the same path "continue from the parts
    /// list" already takes.
    /// </remarks>
    [AvaloniaFact]
    public void A_run_whose_parts_do_not_fit_offers_a_scale_that_would()
    {
        using var run = ARunWithAPartTooBig();

        run.HasPartsThatDoNotFit.Should().BeTrue();
        run.DoesNotFitText.Should().Contain("168");

        run.Parts.Single(p => p.PartNumber == "46891").DoesNotFitThePlate.Should().BeTrue();
        run.Parts.Single(p => p.PartNumber == "32523").DoesNotFitThePlate.Should().BeFalse();

        RunSettings? asked = null;
        run.ContinueRequested += (_, settings) => asked = settings;

        run.TryASmallerScaleCommand.Execute(null);

        asked.Should().NotBeNull();
        asked!.ScalePercent.Should().Be(168);
        asked.Kind.Should().Be(InputKind.PartsList);
    }

    /// <summary>A run where everything fits offers nothing.</summary>
    [AvaloniaFact]
    public void A_run_whose_parts_all_fit_offers_nothing()
    {
        using var run = APretendRun();

        run.HasPartsThatDoNotFit.Should().BeFalse();
    }
```

with this helper beside the others in the same file:

```csharp
    private static RunDocumentViewModel ARunWithAPartTooBig()
    {
        var layout = RunLayout.For(Path.Combine(
            Path.GetTempPath(), "lego2stl-toobig-" + Guid.NewGuid().ToString("N"), "parts.csv"));

        layout.CreateDirectories();
        File.WriteAllText(layout.PartsListPath, "a parts list");

        var entries = new[]
        {
            new PartEntry(1, "32523", 11, "Black", Rgb24.Parse("#05131D"), 4),
            new PartEntry(2, "46891", 11, "Black", Rgb24.Parse("#05131D"), 1),
        };

        var outcome = new RunOutcome
        {
            Result = RunResult.Complete,
            Settings = new RunSettings
            {
                Kind = InputKind.PartsList,
                InputPath = layout.PartsListPath,
                Offline = true,
                ScalePercent = 200,
            },
            Layout = layout,
            PartsList = new PartsList(entries, []),
            Plates = new PlateBuildResult(
                [],
                [new SkippedPart("46891", 304f, 184.8f, 192.2f, TooTall: false)]),
        };

        var manifest = RunManifest.From(
            outcome, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null) with
        {
            LargestFittingScalePercent = 168,
        };

        return RunDocumentViewModel.Of(RunDocument.From(manifest, layout));
    }
```

- [ ] **Step 2: Run and watch it fail**

```
dotnet test tests/Lego2STL.UiTests/Lego2STL.UiTests.csproj --filter "FullyQualifiedName~do_not_fit"
```

Expected: FAIL to compile.

- [ ] **Step 3: Carry the skipped parts into the record**

In `src/Lego2STL.Core/Run/RunManifest.cs`, beside `Plates`:

```csharp
    /// <summary>Parts no plate could take, with what ruled each one out.</summary>
    public IReadOnlyList<SkippedPart> DidNotFit { get; init; } = [];
```

In `RunManifest.From`:

```csharp
            DidNotFit = outcome.Plates?.Skipped ?? [],
```

Mirror on `RunDocument` and its projection:

```csharp
    public IReadOnlyList<SkippedPart> DidNotFit { get; init; } = [];
```

```csharp
            DidNotFit = manifest.DidNotFit,
```

- [ ] **Step 4: Add the wording**

`TextKey.cs`:

```csharp
    /// <summary>Said of a part no plate could take.</summary>
    UiDoesNotFitThePlate,

    /// <summary>The band offering a scale at which everything would fit.</summary>
    UiSomePartsDoNotFit,

    /// <summary>The button on that band.</summary>
    UiTryASmallerScale,
```

`Strings.English.cs`:

```csharp
            [TextKey.UiDoesNotFitThePlate] = "Too big for the plate, so it is on none.",
            [TextKey.UiSomePartsDoNotFit] =
                "{0} part(s) do not fit the plate. Everything fits at {1}%.",
            [TextKey.UiTryASmallerScale] = "Start again at {0}%",
```

`Strings.Italian.cs`:

```csharp
            [TextKey.UiDoesNotFitThePlate] = "Troppo grande per il piano, quindi non è su nessuno.",
            [TextKey.UiSomePartsDoNotFit] =
                "{0} pezzo/i non entrano nel piano. Alla scala {1}% entra tutto.",
            [TextKey.UiTryASmallerScale] = "Riparti al {0}%",
```

- [ ] **Step 5: Let the card and the page know**

In `CataloguePartViewModel`, add a constructor parameter and a property. The constructor
currently takes `(RunDocumentPart part, string? shapePath, string? platePath)`; add a fourth:

```csharp
    public CataloguePartViewModel(
        RunDocumentPart part, string? shapePath, string? platePath, bool doesNotFitThePlate = false)
```

```csharp
        DoesNotFitThePlate = doesNotFitThePlate;
```

```csharp
    /// <summary>True when no plate could take this part at the scale the run used.</summary>
    public bool DoesNotFitThePlate { get; }
```

and add it to `Warnings`, after the self-intersection block:

```csharp
            if (DoesNotFitThePlate)
            {
                warnings.Add(Localization.Loc.Current.Text(Core.Text.TextKey.UiDoesNotFitThePlate));
            }
```

`HasWarning` reads from `RunDocumentPart`, which does not know about plates, so override it on
the card:

```csharp
    public bool HasWarning => Part.HasWarning || DoesNotFitThePlate;
```

In `RunCatalogue.Build`, pass it — the document now carries the list:

```csharp
        var tooBig = document.DidNotFit
            .Select(part => part.PartNumber)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
```

```csharp
                return new CataloguePartViewModel(
                    part,
                    File.Exists(shape) ? shape : null,
                    PlateFor(document, plates, part),
                    tooBig.Contains(part.PartNumber));
```

In `RunDocumentViewModel`, beside `CanContinue`:

```csharp
    public bool HasPartsThatDoNotFit =>
        Document.DidNotFit.Count > 0 && Document.LargestFittingScalePercent is not null;

    public string DoesNotFitText => Loc.Current.Format(
        TextKey.UiSomePartsDoNotFit,
        Document.DidNotFit.Count,
        Document.LargestFittingScalePercent ?? 0);

    public string TryASmallerScaleText => Loc.Current.Format(
        TextKey.UiTryASmallerScale, Document.LargestFittingScalePercent ?? 0);

    /// <summary>
    /// Starts again from this run's parts list at the largest scale everything fits at.
    /// </summary>
    /// <remarks>
    /// The same road "continue from the parts list" takes, so the second run lands in the folder
    /// the first one used rather than beside it.
    /// </remarks>
    [RelayCommand]
    private void TryASmallerScale()
    {
        if (Document.LargestFittingScalePercent is not { } scale)
        {
            return;
        }

        if (!File.Exists(Document.PartsListPath))
        {
            return;
        }

        ContinueRequested?.Invoke(this, (Document.Settings ?? new RunSettings()) with
        {
            Kind = InputKind.PartsList,
            InputPath = Document.PartsListPath,
            SetNumber = null,
            Pages = null,
            ScalePercent = scale,
        });
    }
```

Add `HasPartsThatDoNotFit`, `DoesNotFitText` and `TryASmallerScaleText` to the names raised in
`Replace` and in `Reword`.

- [ ] **Step 6: Show the band**

In `src/Lego2STL.Gui/Views/CatalogueView.axaml`, inside the top `StackPanel`, after the
filter row:

```xml
      <!-- Said once, above the cards, because it is about the run rather than about one part. -->
      <Border Padding="12,10" CornerRadius="4"
              Background="{DynamicResource AppWarningBand}"
              BorderBrush="{DynamicResource AppWarningBorder}"
              BorderThickness="1"
              IsVisible="{Binding HasPartsThatDoNotFit}">
        <Grid ColumnDefinitions="*,Auto">
          <TextBlock Grid.Column="0" TextWrapping="Wrap" VerticalAlignment="Center"
                     Foreground="{DynamicResource AppWarningText}"
                     Text="{Binding DoesNotFitText}" />
          <Button Grid.Column="1" Margin="12,0,0,0" VerticalAlignment="Center"
                  Command="{Binding TryASmallerScaleCommand}"
                  Content="{Binding TryASmallerScaleText}" />
        </Grid>
      </Border>
```

- [ ] **Step 7: Run the whole suite**

```
dotnet test Lego2STL.slnx
```

Expected: PASS.

- [ ] **Step 8: Commit**

```bash
git add -A
git commit -m "feat: the catalogue offers a scale at which every part fits the plate"
```

---

### Task 8: The element number survives the run

**Files:**
- Modify: `src/Lego2STL.Core/Ocr/CatalogueReader.cs` (`CatalogueReading`)
- Modify: `src/Lego2STL.Core/Catalogue/PartEntry.cs`
- Modify: `src/Lego2STL.Core/Catalogue/PartsList.cs` (`PartsListBuilder.Build`)
- Modify: `src/Lego2STL.Core/Pipeline/PipelineRunner.cs:360-369`
- Modify: `src/Lego2STL.Core/Run/RunManifest.cs`, `src/Lego2STL.Core/Run/RunDocument.cs`
- Test: `tests/Lego2STL.Tests/Catalogue/PartsListBuilderTests.cs`

**Interfaces:**
- Consumes: `PrintedEntry.ElementId` (`string`), already read by `PrintedCatalogue`.
- Produces: `CatalogueReading.ElementId` (`string?`, trailing optional),
  `PartEntry.ElementId` (`string?`, trailing optional), `ManifestPart.ElementId` (`string?`),
  `RunDocumentPart.ElementId` (`string?`). Task 9 shows it.

- [ ] **Step 1: Write the failing test**

Add to `tests/Lego2STL.Tests/Catalogue/PartsListBuilderTests.cs`:

```csharp
    /// <summary>
    /// The element number a book printed is kept, because it is what a part is bought by.
    /// </summary>
    /// <remarks>
    /// It used to be read, turned into a part and a colour, and dropped - so the one number
    /// actually printed in the instructions was the one number the run could not show.
    /// </remarks>
    [Fact]
    public void An_entry_read_from_an_element_number_remembers_it()
    {
        var readings = new[]
        {
            new CatalogueReading(
                370, new PixelBounds(0, 0, 10, 10), 7, "32523", 11,
                ReadingSource.PrintedText, ReadingSource.PrintedText,
                ColorScheme.BrickLink, ElementId: "6177114"),
        };

        var list = PartsListBuilder.Build(readings, ColorReference.Table, ColorScheme.BrickLink);

        list.Entries.Should().ContainSingle().Which.ElementId.Should().Be("6177114");
    }

    /// <summary>A list read from a CSV has none, and says so rather than inventing one.</summary>
    [Fact]
    public void An_entry_read_without_an_element_number_has_none()
    {
        var readings = new[]
        {
            new CatalogueReading(
                2, new PixelBounds(0, 0, 10, 10), 4, "3705", 5,
                ReadingSource.Recognised, ReadingSource.Recognised),
        };

        var list = PartsListBuilder.Build(readings, ColorReference.Table, ColorScheme.BrickLink);

        list.Entries.Should().ContainSingle().Which.ElementId.Should().BeNull();
    }
```

- [ ] **Step 2: Run and watch it fail**

```
dotnet test tests/Lego2STL.Tests/Lego2STL.Tests.csproj --filter "FullyQualifiedName~element_number"
```

Expected: FAIL to compile.

- [ ] **Step 3: Thread it through**

`CatalogueReader.cs` — append to `CatalogueReading`:

```csharp
    ColorScheme? Scheme = null,
    string? ElementId = null)
```

`PartEntry.cs` — append, with the doc comment:

```csharp
/// <param name="ElementId">
/// The LEGO element number the entry was read from, when it was read from one. Null for a list
/// that came from a CSV or from a set, which name a part and a colour rather than an element.
/// </param>
public sealed record PartEntry(
    int Id,
    string PartNumber,
    int BrickLinkColorCode,
    string ColorName,
    Rgb24 Rgb,
    int Quantity,
    string? ElementId = null)
```

`PartsList.cs` — in `PartsListBuilder.Build`, the `merged.Add` call:

```csharp
            merged.Add(new PartEntry(
                Id: 0,                       // numbered below, once the order is final
                PartNumber: reading.PartNumber,
                BrickLinkColorCode: brickLinkId,
                ColorName: color.Name,
                Rgb: color.Rgb,
                Quantity: reading.Quantity,
                ElementId: reading.ElementId));
```

`PipelineRunner.cs` — the `entries.Add(new CatalogueReading(...))` in `ReadPrintedPagesAsync`:

```csharp
            entries.Add(new CatalogueReading(
                pageNumber,
                entry.Bounds,
                entry.Quantity,
                resolved.PartNumber,
                resolved.ColorCode,
                ReadingSource.PrintedText,
                ReadingSource.PrintedText,
                resolved.Scheme,
                entry.ElementId));
```

`RunManifest.cs` — append to `ManifestPart` and to the `Part` factory:

```csharp
    int? OverusedEdgeCount = null,
    float? ClosedAtTolerance = null,
    string? ElementId = null);
```

```csharp
            shape?.Quality.OverusedEdgeCount,
            shape?.ClosedAtTolerance,
            entry.ElementId);
```

`RunDocument.cs` — the same on `RunDocumentPart` and its projection:

```csharp
    int? OverusedEdgeCount = null,
    float? ClosedAtTolerance = null,
    string? ElementId = null)
```

```csharp
                    part.OverusedEdgeCount,
                    part.ClosedAtTolerance,
                    part.ElementId)),
```

- [ ] **Step 4: Run the whole suite**

```
dotnet test Lego2STL.slnx
```

Expected: PASS. `PartsListCsv` is untouched, so the six-column schema and its round-trip tests
are unaffected.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat: a run keeps the element number the instructions printed"
```

---

### Task 9: The numbering menu

**Files:**
- Modify: `src/Lego2STL.Gui/ViewModels/CataloguePartViewModel.cs`
- Modify: `src/Lego2STL.Gui/ViewModels/RunDocumentViewModel.cs`
- Modify: `src/Lego2STL.Gui/Services/UserSettings.cs`
- Modify: `src/Lego2STL.Gui/Views/CatalogueView.axaml`
- Modify: `src/Lego2STL.Core/Text/TextKey.cs`, `Strings.English.cs`, `Strings.Italian.cs`
- Test: `tests/Lego2STL.UiTests/CatalogueTests.cs`

**Interfaces:**
- Consumes: `RunDocumentPart.ElementId` from Task 8.
- Produces: `RunDocumentViewModel.Numbering` (`PartNumbering`), `.Numberings`;
  `CataloguePartViewModel.ShownNumber` (`string`); `enum PartNumbering { BrickLink, LegoElement }`
  in `Lego2STL.Gui.ViewModels`.

- [ ] **Step 1: Write the failing test**

Add to `tests/Lego2STL.UiTests/CatalogueTests.cs`:

```csharp
    /// <summary>The catalogue can show either numbering, and says when it has none.</summary>
    [AvaloniaFact]
    public void The_catalogue_shows_either_numbering()
    {
        using var run = ARunWithElementNumbers();

        var withOne = run.Parts.Single(p => p.PartNumber == "32523");
        var without = run.Parts.Single(p => p.PartNumber == "3705");

        withOne.ShownNumber.Should().Be("32523");

        run.Numbering = PartNumbering.LegoElement;

        withOne.ShownNumber.Should().Be("6177114");
        without.ShownNumber.Should().Be(
            Loc.Current.Text(TextKey.UiNoElementNumber),
            "a list from a CSV has no element numbers and must not invent one");

        run.Numbering = PartNumbering.BrickLink;
        withOne.ShownNumber.Should().Be("32523");
    }

    private static RunDocumentViewModel ARunWithElementNumbers()
    {
        var layout = RunLayout.For(Path.Combine(
            Path.GetTempPath(), "lego2stl-numbering-" + Guid.NewGuid().ToString("N"), "parts.csv"));

        var entries = new[]
        {
            new PartEntry(1, "32523", 11, "Black", Rgb24.Parse("#05131D"), 4, ElementId: "6177114"),
            new PartEntry(2, "3705", 5, "Red", Rgb24.Parse("#C91A09"), 12),
        };

        var outcome = new RunOutcome
        {
            Result = RunResult.Complete,
            Settings = new RunSettings { Kind = InputKind.PartsList, InputPath = "parts.csv", Offline = true },
            Layout = layout,
            PartsList = new PartsList(entries, []),
        };

        return RunDocumentViewModel.Of(RunDocument.From(
            RunManifest.From(outcome, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null), layout));
    }
```

- [ ] **Step 2: Run and watch it fail**

```
dotnet test tests/Lego2STL.UiTests/Lego2STL.UiTests.csproj --filter "FullyQualifiedName~either_numbering"
```

Expected: FAIL to compile.

- [ ] **Step 3: Add the wording**

`TextKey.cs`:

```csharp
    /// <summary>The catalogue's choice of which numbering to show.</summary>
    UiNumbering,

    /// <summary>Shown in place of an element number for a list that has none.</summary>
    UiNoElementNumber,
```

`Strings.English.cs`:

```csharp
            [TextKey.UiNumbering] = "Numbering",
            [TextKey.UiNoElementNumber] = "no element number",
```

`Strings.Italian.cs`:

```csharp
            [TextKey.UiNumbering] = "Numerazione",
            [TextKey.UiNoElementNumber] = "nessun numero elemento",
```

- [ ] **Step 4: Add the choice and the shown number**

Create the enum and its wording at the top of
`src/Lego2STL.Gui/ViewModels/CataloguePartViewModel.cs`:

```csharp
/// <summary>Which of a part's two numbers the catalogue shows.</summary>
public enum PartNumbering
{
    /// <summary>The part number, which is what the shape files are named after.</summary>
    BrickLink,

    /// <summary>The LEGO element number, which names a moulding and a colour together.</summary>
    LegoElement,
}
```

In `CataloguePartViewModel`, add an observable choice and the derived number:

```csharp
    /// <summary>Which numbering to show. Set by the page, which owns the choice.</summary>
    [ObservableProperty]
    public partial PartNumbering Numbering { get; set; } = PartNumbering.BrickLink;

    partial void OnNumberingChanged(PartNumbering value) => OnPropertyChanged(nameof(ShownNumber));

    /// <summary>
    /// The number on the card, in whichever numbering was asked for.
    /// </summary>
    /// <remarks>
    /// A list read from a CSV or from a set has no element numbers, and says so rather than
    /// showing a blank that reads as missing data.
    /// </remarks>
    public string ShownNumber => Numbering switch
    {
        PartNumbering.LegoElement => Part.ElementId
                                     ?? Localization.Loc.Current.Text(Core.Text.TextKey.UiNoElementNumber),
        _ => PartNumber,
    };
```

In `RunDocumentViewModel`, beside `ColourFilter`:

```csharp
    /// <summary>Which numbering the catalogue shows, remembered between sessions.</summary>
    [ObservableProperty]
    public partial PartNumbering Numbering { get; set; } = PartNumbering.BrickLink;

    partial void OnNumberingChanged(PartNumbering value)
    {
        foreach (var part in Parts)
        {
            part.Numbering = value;
        }
    }
```

and in `Fill()`, after the parts are added, apply the current choice to the new cards:

```csharp
        foreach (var part in Parts)
        {
            part.Numbering = Numbering;
        }
```

- [ ] **Step 5: Remember it**

In `src/Lego2STL.Gui/Services/UserSettings.cs`, beside `Printer`:

```csharp
    /// <summary>Which numbering the catalogue last showed.</summary>
    [JsonPropertyName("partNumbering")]
    public string? PartNumbering { get; set; }
```

`MainViewModel` holds the one `UserSettings` instance in the field `_saved` and already funnels
every page through `Open(RunDocumentViewModel)`. Put the reading and the writing there, so a run
document stays unaware of preferences:

```csharp
    /// <summary>
    /// Hands a new page the choices that outlive a single run, and keeps them when they change.
    /// </summary>
    /// <remarks>
    /// Here rather than on the page, because a run document is about one run and this is about
    /// every run. The parse is forgiving: an unreadable preference is one worth losing.
    /// </remarks>
    private void RememberNumbering(RunDocumentViewModel page)
    {
        page.Numbering = Enum.TryParse<PartNumbering>(_saved.PartNumbering, out var saved)
            ? saved
            : PartNumbering.BrickLink;

        page.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName != nameof(RunDocumentViewModel.Numbering))
            {
                return;
            }

            _saved.PartNumbering = page.Numbering.ToString();
            _saved.Save();
        };
    }
```

Call it from `Open`, which is `private void Open(RunDocumentViewModel run)` at
`MainViewModel.cs:197` — the single method both `Runs.OpenRequested` and `BeginAsync` route
through. Add `RememberNumbering(run);` as its first statement.

- [ ] **Step 6: Show the menu**

In `src/Lego2STL.Gui/Views/CatalogueView.axaml`, in the filter row after the search box:

```xml
        <TextBlock Classes="label" VerticalAlignment="Center"
                   Text="{Binding [UiNumbering], Source={x:Static loc:Loc.Current}}" />
        <ComboBox MinWidth="170" SelectedItem="{Binding Numbering}"
                  ItemsSource="{Binding Numberings}" />
```

and expose the two choices on `RunDocumentViewModel`:

```csharp
    /// <summary>The two numberings, for the menu that chooses between them.</summary>
    public static IReadOnlyList<PartNumbering> Numberings { get; } =
        [PartNumbering.BrickLink, PartNumbering.LegoElement];
```

Bind the card's number to the choice — in the same file, replace the part-number `TextBlock`:

```xml
                  <TextBlock Grid.Column="1" FontWeight="SemiBold"
                             Text="{Binding ShownNumber}" TextTrimming="CharacterEllipsis" />
```

- [ ] **Step 7: Run the whole suite**

```
dotnet test Lego2STL.slnx
```

Expected: PASS.

- [ ] **Step 8: Look at it**

```bash
LEGO2STL_UI_SHOTS=/tmp/shots dotnet test tests/Lego2STL.UiTests/Lego2STL.UiTests.csproj --filter "FullyQualifiedName~A_picture_of_a_filled_catalogue"
```

Open `/tmp/shots/Catalogue-filled.png` and check the menu sits in the filter row without
crowding it. Ask the user to confirm before considering this done.

- [ ] **Step 9: Commit**

```bash
git add -A
git commit -m "feat: the catalogue can show LEGO element numbers instead of part numbers"
```

---

## Notes for whoever executes this

- **Task 1 before Task 2.** The escalation is tuned against a measurement that Task 1 changes.
- Tasks 5–7 and Tasks 8–9 are independent of each other and of Tasks 1–3.
- If any task's test passes *before* the implementation step, stop: either the test is not
  testing what it claims or the behaviour is already there. Do not proceed on a green that was
  never red.
- Record `PHASE:LOT-B WAVE:0 STATUS:complete TS:<ISO-8601-UTC>` in `PROGRESS.md` when all nine
  are done.
