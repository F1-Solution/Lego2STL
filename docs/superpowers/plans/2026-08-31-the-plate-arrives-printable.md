# The Plate Arrives Printable — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A run that writes plates also writes the settings to print them with, and lays each part
down the way its kind should be printed — so a plate opens in a slicer ready rather than merely
correct.

**Architecture:** Two halves of one question. A part's kind is read from its LDraw description and
decides how it is laid on the bed, between the existing `StandUp` and `SitOnBed` stages. Then, beside
the plates, the run writes a thin Bambu process preset that inherits from the printer's own base and
asserts only what the tool knows, plus an instruction sheet in the run's language carrying everything
the preset deliberately does not assert.

**Tech Stack:** C# / .NET 10, xUnit + FluentAssertions, System.Text.Json, System.Numerics.

**Spec:** `docs/superpowers/specs/2026-08-31-the-plate-arrives-printable-design.md`

## Global Constraints

- Build with `dotnet build Lego2STL.slnx -c Debug`. Test with `dotnet test Lego2STL.slnx`.
- Every user-facing string goes through `TextKey` and is added to **both** `Strings.English.cs`
  and `Strings.Italian.cs`. The suite walks every key in both languages and fails on a gap.
- Code comments and CHANGELOG entries: **one sentence each**. Test comments are exempt.
- Commit messages: `<type>: <description>`, describing observable behaviour, never internal class
  or method names.
- Files stay under 800 lines; functions under 50.
- Source files are UTF-8 **with** a byte-order mark and CRLF, like every other `.cs` here.
- **Every value in a Bambu preset JSON is a string**, never a number or a bool. Per-extruder
  settings — the speeds — are **arrays of one string** on a single-extruder machine:
  `"outer_wall_speed": ["35"]`. Verified against `0.08mm High Quality @BBL A1.json` on 2026-08-31.
- **Nothing is written for a run that asked for `--no-plates`.** These files are about printing a
  plate, and there is no plate.
- **The elephant-foot value never enters the mesh.** It is a preset key and only a preset key.

---

### Task 1: A part's kind, from the description LDraw already gave it

**Files:**
- Create: `src/Lego2STL.Core/Geometry/PartKind.cs`
- Test: `tests/Lego2STL.Tests/Geometry/PartKindTests.cs` (create)

**Interfaces:**
- Consumes: nothing.
- Produces: `enum PartKind { Unknown, Brick, Plate, Tile, Beam, Axle, Pin }` and
  `static class PartKinds` with `FromTitle(string? title) → PartKind`.

The titles in the test come from run 6324712's own record. 150 of its 219 titles begin with
`Technic`, so the kind is frequently the second word rather than the first — that is the case this
reader exists to get right.

- [ ] **Step 1: Write the failing tests**

Create `tests/Lego2STL.Tests/Geometry/PartKindTests.cs`:

```csharp
using FluentAssertions;
using Lego2STL.Core.Geometry;

namespace Lego2STL.Tests.Geometry;

/// <summary>
/// Reading what a part is out of the description LDraw gives it.
/// </summary>
/// <remarks>
/// Every title here is a real one, taken from the record of run 6324712. The awkward case is
/// Technic: two thirds of that run's parts begin with the word, so the kind is the second word,
/// and a reader that only looks at the first finds nothing at all for most of a real set.
/// </remarks>
public sealed class PartKindTests
{
    [Theory]
    [InlineData("Brick  2 x  4", PartKind.Brick)]
    [InlineData("Technic Brick  1 x  2 with Hole", PartKind.Brick)]
    [InlineData("Plate  6 x  8", PartKind.Plate)]
    [InlineData("Plate  2 x  2 with Holes", PartKind.Plate)]
    [InlineData("Tile  1 x  2 Grille with Bottom Groove", PartKind.Tile)]
    [InlineData("Technic Beam  3 x  0.5 Liftarm", PartKind.Beam)]
    [InlineData("Technic Beam 15", PartKind.Beam)]
    [InlineData("Technic Axle  4", PartKind.Axle)]
    [InlineData("Technic Axle 12", PartKind.Axle)]
    [InlineData("Technic Pin Long with Friction Ridges", PartKind.Pin)]
    [InlineData("Technic Pin Joiner Perpendicular", PartKind.Pin)]
    public void A_description_says_what_the_part_is(string title, PartKind expected) =>
        PartKinds.FromTitle(title).Should().Be(expected);

    /// <summary>
    /// An axle pin is a pin, because it is the pin end that decides how it lies.
    /// </summary>
    /// <remarks>
    /// It reads as both, and the order the reader tries its words in is what settles it. Written
    /// down as a test because the answer is a choice, not a fact, and the next person should find
    /// the choice rather than re-make it.
    /// </remarks>
    [Fact]
    public void An_axle_pin_is_a_pin() =>
        PartKinds.FromTitle("Technic Axle Pin  3L with Friction").Should().Be(PartKind.Pin);

    [Theory]
    [InlineData("Technic Cross Block  1 x  3")]
    [InlineData("Technic Gear 20 Tooth")]
    [InlineData("Technic Panel  5 x 11")]
    [InlineData("Technic Turntable 60 Tooth Bottom")]
    [InlineData("Slope Brick 45  2 x  2")]
    [InlineData("Bar  3L")]
    [InlineData("Bracket  1 x  2 -  2 x  2 Down")]
    [InlineData("Wheel Rim 16 x 31 with 6 Pegholes")]
    [InlineData("Electric Control+ L Motor")]
    public void A_kind_with_no_rule_is_not_guessed_at(string title) =>
        PartKinds.FromTitle(title).Should().Be(PartKind.Unknown);

    /// <summary>Two fifths of a real set has no rule, so nothing may depend on there being one.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("~Moved to 3023b")]
    public void Nothing_to_read_is_not_a_kind(string? title) =>
        PartKinds.FromTitle(title).Should().Be(PartKind.Unknown);
}
```

- [ ] **Step 2: Run them and watch them fail**

```
dotnet test tests/Lego2STL.Tests/Lego2STL.Tests.csproj -f net10.0-windows10.0.19041.0 --filter "FullyQualifiedName~PartKind"
```

Expected: FAIL to compile — `PartKind` does not exist.

- [ ] **Step 3: Write it**

Create `src/Lego2STL.Core/Geometry/PartKind.cs`:

```csharp
namespace Lego2STL.Core.Geometry;

/// <summary>The kinds of part this tool has a rule for, and one for everything else.</summary>
public enum PartKind
{
    Unknown,
    Brick,
    Plate,
    Tile,
    Beam,
    Axle,
    Pin,
}

/// <summary>
/// Reads what a part is out of the description its LDraw file carries.
/// </summary>
/// <remarks>
/// <para>
/// Not from the parts database. That reads from a Rebrickable bulk download whose
/// <c>inventory_parts.csv</c> alone is 132 MB, which is never committed, so most runs have no
/// category for anything. The description is already in hand for every part that produced a shape.
/// </para>
/// <para>
/// Deliberately incomplete. Measured over run 6324712, these kinds cover about three fifths of a
/// real set and the rest comes back unknown, which is a verdict rather than a failure: a part with
/// no rule is left exactly as the pipeline already leaves it.
/// </para>
/// </remarks>
public static class PartKinds
{
    /// <summary>
    /// Ordered, because a title can read as two kinds and the first match wins.
    /// </summary>
    /// <remarks>
    /// Pin before axle: an "Axle Pin" is a pin with an axle on the end, and it is the pin that
    /// decides how it lies. Tile and plate before brick for the same reason - "Tile" and "Plate"
    /// are their own kinds and neither is a low brick as far as printing is concerned.
    /// </remarks>
    private static readonly (string Word, PartKind Kind)[] Words =
    [
        ("pin", PartKind.Pin),
        ("axle", PartKind.Axle),
        ("beam", PartKind.Beam),
        ("tile", PartKind.Tile),
        ("plate", PartKind.Plate),
        ("brick", PartKind.Brick),
    ];

    /// <summary>What the part is, or unknown when its description does not say.</summary>
    public static PartKind FromTitle(string? title)
    {
        if (string.IsNullOrWhiteSpace(title) || title.StartsWith('~'))
        {
            return PartKind.Unknown;
        }

        // Only the words before the first measurement: "Technic Axle 4" is an axle, while
        // "Technic Panel 5 x 11" must not become a plate because of a later "Plate" in some
        // longer description.
        var head = new List<string>();

        foreach (var word in title.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (!char.IsLetter(word[0]))
            {
                break;
            }

            head.Add(word);
        }

        foreach (var (word, kind) in Words)
        {
            if (head.Any(w => string.Equals(w, word, StringComparison.OrdinalIgnoreCase)))
            {
                return kind;
            }
        }

        return PartKind.Unknown;
    }
}
```

- [ ] **Step 4: Run the tests**

```
dotnet test tests/Lego2STL.Tests/Lego2STL.Tests.csproj -f net10.0-windows10.0.19041.0 --filter "FullyQualifiedName~PartKind"
```

Expected: PASS. If `Technic Beam  3 x  0.5 Liftarm` fails, the head-word loop is stopping too early
or too late — the head for that title must be `Technic Beam`.

- [ ] **Step 5: Commit**

```bash
git add src/Lego2STL.Core/Geometry/PartKind.cs tests/Lego2STL.Tests/Geometry/PartKindTests.cs
git commit -m "feat: a part's description says what kind of part it is"
```

---

### Task 2: A part that only redirects keeps the description of the part it becomes

**Files:**
- Modify: `src/Lego2STL.Core/LDraw/LDrawMeshBuilder.cs:60-82`
- Test: `tests/Lego2STL.Tests/LDraw/LDrawMeshBuilderTests.cs` (add to it; create if absent)

**Interfaces:**
- Consumes: `PartMesh(Reference, Title, Triangles, MovedTo, FilesUsed, MissingReferences)`, unchanged.
- Produces: nothing new. `PartMesh.Title` simply stops being a redirect stub.

Four parts of run 6324712 carry `~Moved to 3023b` and the like as their title. The geometry is right
— the builder follows the redirect when it expands the file — but `root.Title` is the stub's own
description, so the kind reader from Task 1 gets nothing for them.

- [ ] **Step 1: Write the failing test**

Add to `tests/Lego2STL.Tests/LDraw/LDrawMeshBuilderTests.cs`, using the `FakeLDrawLibrary` the file
already uses — it takes files by name with `.Add(name, content)`:

```csharp
    /// <summary>
    /// A part that is only a redirection is described by the part it redirects to.
    /// </summary>
    /// <remarks>
    /// The mesh was always right - the builder follows the redirection when it expands the file -
    /// but the description recorded was the stub's own, "~Moved to 3023b", which says nothing
    /// about what the part is. Four of run 6324712's parts are like this, and the reader that
    /// works out what kind of part it is has only that description to go on.
    /// </remarks>
    [Fact]
    public async Task A_part_that_only_redirects_is_described_by_the_part_it_becomes()
    {
        var library = new FakeLDrawLibrary()
            .Add("3023.dat", "0 ~Moved to 3023b
1 16 0 0 0 1 0 0 0 1 0 0 0 1 3023b.dat
")
            .Add("3023b.dat", "0 Plate  1 x  2
3 16 0 0 0  10 0 0  0 10 0
");

        var mesh = await new LDrawMeshBuilder(library).BuildAsync("3023");

        mesh.MovedTo.Should().Be("3023b");
        mesh.Title.Should().Be("Plate  1 x  2");
        mesh.Triangles.Should().ContainSingle("the geometry always came from the part it points at");
    }
```

- [ ] **Step 2: Run it and watch it fail**

```
dotnet test tests/Lego2STL.Tests/Lego2STL.Tests.csproj -f net10.0-windows10.0.19041.0 --filter "FullyQualifiedName~only_redirects_is_described"
```

Expected: FAIL — the title comes back as `~Moved to 3023b`.

- [ ] **Step 3: Take the title from the part actually built**

In `LDrawMeshBuilder.BuildAsync`, after reading `root` and before constructing the `PartMesh`:

```csharp
        // A redirection describes itself as a redirection. What the caller wants to know is what
        // the part it points at is, which is also the shape that was just built.
        var title = root.Title;

        if (root.MovedTo is { Length: > 0 } replacement)
        {
            var target = await ReadAsync(replacement + ".dat", cancellationToken).ConfigureAwait(false);
            title = target?.Title ?? title;
        }
```

and pass `title` in place of `root.Title` in the `new PartMesh(...)` below it.

- [ ] **Step 4: Run the whole suite**

```
dotnet test Lego2STL.slnx
```

Expected: PASS. A chain of two redirections resolves only one step, which is deliberate and enough:
LDraw does not chain them, and a loop cannot then hang the build.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "fix: a part that only redirects is described by the part it becomes"
```

---

### Task 3: The gate — does rolling an axle actually help?

**Files:**
- Create: `<scratchpad>/roll/Program.cs` and a minimal `.csproj`, deleted at the end of this task
- Modify: nothing in `src/` or `tests/`

**Interfaces:**
- Consumes: the `.stl` files of run 6324712.
- Produces: a yes or a no. **If it is a no, Tasks 4 and 5 drop the axle rule and ship only the
  confirming rules, and the spec is corrected to say so.**

The axle roll is the one rule in this design that comes from practice rather than from a
measurement. The earlier spike could not have found it — it tried only the six axis-aligned
orientations, and a `+` cross section is symmetric under every roll that set contains, so all of
them were no-ops. This task settles it before any code depends on it.

- [ ] **Step 1: Write the probe**

A console project in the scratchpad that reads a binary STL — 80 bytes of header, a little-endian
`uint` count, then 50 bytes per triangle — and scores the area of faces pointing more than 45° below
horizontal, **excluding any triangle whose highest vertex is within 0.2 mm of the lowest point of
the whole mesh**, because the face a part rests on points straight down too and counting it is what
made the earlier probe advise standing plates on edge.

```csharp
static (double Total, double Over) Score(List<(Vector3 A, Vector3 B, Vector3 C)> t, Matrix4x4 turn)
{
    var limit = MathF.Cos(45f * MathF.PI / 180f);
    var floor = float.MaxValue;

    foreach (var (a, b, c) in t)
    {
        foreach (var v in new[] { a, b, c })
        {
            floor = MathF.Min(floor, Vector3.Transform(v, turn).Z);
        }
    }

    double total = 0, over = 0;

    foreach (var (a, b, c) in t)
    {
        var (ta, tb, tc) = (Vector3.Transform(a, turn), Vector3.Transform(b, turn), Vector3.Transform(c, turn));
        var raw = Vector3.Cross(tb - ta, tc - ta);
        var area = raw.Length() / 2f;

        if (area <= 0)
        {
            continue;
        }

        total += area;

        if (Vector3.Normalize(raw).Z < -limit
            && MathF.Max(ta.Z, MathF.Max(tb.Z, tc.Z)) - floor > 0.2f)
        {
            over += area;
        }
    }

    return (total, over);
}
```

- [ ] **Step 2: Score six real axles flat and rolled**

The axles in the run lie along X, so the roll is about X. Print, for `3705`, `32073`, `3706`,
`3707`, `3737` and `3708`, the overhanging fraction at `Matrix4x4.Identity` and at
`Matrix4x4.CreateRotationX(MathF.PI / 4f)`.

Run it:

```
dotnet run -c Release -- C:/Progetti/Lego2STL/6324712/stl
```

- [ ] **Step 3: Decide, and write the number down**

The rule ships only if the rolled fraction is lower for **every one of the six**. A rule that helps
five axles and hurts one is not a rule about axles.

Record the six pairs of numbers in the spec under the axle rule, replacing the sentence saying the
rule is unverified, whichever way it goes. If the answer is no, say so there plainly and strike the
axle row from the table in the spec before starting Task 4.

- [ ] **Step 4: Delete the probe**

```bash
rm -r <scratchpad>/roll
```

- [ ] **Step 5: Commit the finding**

```bash
git add docs/superpowers/specs/2026-08-31-the-plate-arrives-printable-design.md
git commit -m "docs: whether rolling an axle onto one arm reduces its overhangs"
```

---

### Task 4: Each part is laid down the way its kind is printed

**Files:**
- Create: `src/Lego2STL.Core/Geometry/Orientation.cs`
- Modify: `src/Lego2STL.Core/Geometry/MeshPipeline.cs:7-90` (options), `:158-173` (`Prepare`)
- Test: `tests/Lego2STL.Tests/Geometry/OrientationTests.cs` (create)

**Interfaces:**
- Consumes: `PartKind`, `PartKinds.FromTitle` (Task 1).
- Produces: `static class Orientation` with `For(PartKind kind) → Matrix4x4?` — null meaning leave
  it alone — and `Name(PartKind kind) → string?` giving the rule's own name for the record.
  `MeshPipelineOptions.Orient` (`bool`, default `true`). `PreparedMesh.LaidDown` (`string?`,
  trailing optional).

The roll goes in after `StandUp` and after the clearance, and before `SitOnBed` — in millimetre
space with Z already up, so that `SitOnBed` then drops the rolled shape back onto zero. Putting it
earlier would rotate a shape still in source units and leave the part hovering.

- [ ] **Step 1: Write the failing tests**

Create `tests/Lego2STL.Tests/Geometry/OrientationTests.cs`:

```csharp
using System.Numerics;
using FluentAssertions;
using Lego2STL.Core.Geometry;
using Lego2STL.Core.LDraw;

namespace Lego2STL.Tests.Geometry;

/// <summary>
/// Laying a part down the way its kind is printed.
/// </summary>
/// <remarks>
/// Most of these rules change nothing, and that is the point of them: the measurement that led to
/// this feature found that the parts a geometric score shouts about - plates and panels - are the
/// ones already lying correctly. The rules exist to say so out loud and to stop a later change
/// quietly turning them. The tests that assert nothing moved are therefore the important ones.
/// </remarks>
public sealed class OrientationTests
{
    /// <summary>A bar along X, four units square, standing for a part already lying flat.</summary>
    private static PartMesh ABarCalled(string title)
    {
        var t = new List<Triangle>();

        // A closed box from (0,-2,-2) to (40,2,2), as two triangles per face.
        void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            t.Add(new Triangle(a, b, c));
            t.Add(new Triangle(a, c, d));
        }

        Vector3 V(float x, float y, float z) => new(x, y, z);

        Quad(V(0, -2, -2), V(0, 2, -2), V(0, 2, 2), V(0, -2, 2));
        Quad(V(40, -2, -2), V(40, -2, 2), V(40, 2, 2), V(40, 2, -2));
        Quad(V(0, -2, -2), V(0, -2, 2), V(40, -2, 2), V(40, -2, -2));
        Quad(V(0, 2, -2), V(40, 2, -2), V(40, 2, 2), V(0, 2, 2));
        Quad(V(0, -2, 2), V(0, 2, 2), V(40, 2, 2), V(40, -2, 2));
        Quad(V(0, -2, -2), V(40, -2, -2), V(40, 2, -2), V(0, 2, -2));

        return new PartMesh("test", title, t, null, 1, []);
    }

    private static Vector3 SizeOf(PartMesh part, bool orient) =>
        MeshPipeline.Prepare(part, new MeshPipelineOptions { Orient = orient }).Size;

    private static IReadOnlyList<Vector3> CornersOf(PartMesh part, bool orient) =>
        MeshPipeline.Prepare(part, new MeshPipelineOptions { Orient = orient }).Mesh.Vertices;

    /// <summary>The rule for an axle turns it; every other rule is a rule to do nothing.</summary>
    [Theory]
    [InlineData("Plate  2 x  4")]
    [InlineData("Tile  1 x  2")]
    [InlineData("Brick  2 x  2")]
    [InlineData("Technic Beam 15")]
    [InlineData("Technic Pin Long")]
    [InlineData("Technic Panel  5 x 11")]
    [InlineData("Wheel Rim 16 x 31")]
    public void A_part_whose_rule_confirms_what_the_pipeline_already_did_does_not_move(string title)
    {
        var part = ABarCalled(title);

        // Every corner, not the bounding box: a box compared with itself after a quarter turn
        // measures the same and would let a real rotation through unnoticed.
        CornersOf(part, orient: true).Should().Equal(CornersOf(part, orient: false));
    }

    /// <summary>
    /// An axle is rolled onto one arm of its cross, which is wider across than it was.
    /// </summary>
    /// <remarks>
    /// Measured on the bounding box rather than on the transform, because what matters is what
    /// comes out: a square section rolled 45 degrees measures its diagonal, so a 4-unit bar
    /// becomes about 5.66 across and the same amount tall.
    /// </remarks>
    [Fact]
    public void An_axle_is_rolled_onto_one_arm_of_its_cross()
    {
        var flat = SizeOf(ABarCalled("Technic Axle 10"), orient: false);
        var rolled = SizeOf(ABarCalled("Technic Axle 10"), orient: true);

        rolled.X.Should().BeApproximately(flat.X, 0.01f, "its length does not change");
        rolled.Y.Should().BeGreaterThan(flat.Y * 1.3f);
        rolled.Z.Should().BeGreaterThan(flat.Z * 1.3f);
    }

    /// <summary>Turned off, nothing is laid down at all, whatever the part is.</summary>
    [Fact]
    public void Orientation_can_be_turned_off_entirely()
    {
        var part = ABarCalled("Technic Axle 10");

        MeshPipeline.Prepare(part, new MeshPipelineOptions { Orient = false })
            .LaidDown.Should().BeNull();
    }

    /// <summary>What was decided is recorded, because every other decision here is.</summary>
    [Fact]
    public void How_a_part_was_laid_down_is_recorded()
    {
        MeshPipeline.Prepare(ABarCalled("Technic Axle 10")).LaidDown.Should().NotBeNull();
        MeshPipeline.Prepare(ABarCalled("Wheel Rim 16 x 31")).LaidDown.Should().BeNull();
    }
}
```

- [ ] **Step 2: Run them and watch them fail**

```
dotnet test tests/Lego2STL.Tests/Lego2STL.Tests.csproj -f net10.0-windows10.0.19041.0 --filter "FullyQualifiedName~Orientation"
```

Expected: FAIL to compile — `Orient` and `LaidDown` do not exist.

- [ ] **Step 3: Write the rules**

Create `src/Lego2STL.Core/Geometry/Orientation.cs`:

```csharp
using System.Numerics;

namespace Lego2STL.Core.Geometry;

/// <summary>
/// How a part of a given kind is laid on the bed.
/// </summary>
/// <remarks>
/// <para>
/// Not an optimiser, and deliberately not one. Scoring overhang area over the six axis-aligned
/// orientations was measured over 175 real shapes and rejected: it recommends turning 95 of them
/// and leaves 50 taller than three times their narrowest footprint side, which for a 12L axle
/// means standing 191 mm of part on a 9.6 mm base. The parts such a score shouts loudest about -
/// plates and panels - are the ones already lying correctly.
/// </para>
/// <para>
/// So most of what is here is a rule to do nothing, written down so that it is a decision rather
/// than an accident, and so that the tests can hold it still. The governing rule behind the table
/// is that no support may ever touch a mating surface: a stud, an interior tube, a Technic hole,
/// an axle or a pin. Where orientation and support disagree, orientation moves.
/// </para>
/// </remarks>
public static class Orientation
{
    /// <summary>
    /// How to lay a part of this kind, or null to leave it exactly where the pipeline put it.
    /// </summary>
    /// <remarks>
    /// The axle is the only turn. Its cross section is a plus, so lying flat leaves two arms with
    /// their undersides in the air; rolled a quarter of a right angle it rests on one arm and its
    /// lower faces fall away at 45 degrees, which is the shallowest a printer will bridge. The
    /// roll is about X because that is the axis a part is long along once it is standing up.
    /// </remarks>
    public static Matrix4x4? For(PartKind kind) => kind switch
    {
        PartKind.Axle => Matrix4x4.CreateRotationX(MathF.PI / 4f),
        _ => null,
    };

    /// <summary>The rule's own name, for the record a run keeps. Null when no rule applied.</summary>
    public static string? Name(PartKind kind) => For(kind) is null ? null : kind.ToString();
}
```

- [ ] **Step 4: Give the pipeline the switch and the record**

In `MeshPipelineOptions`, beside the other switches:

```csharp
    /// <summary>Whether to lay each part down the way its kind is printed.</summary>
    public bool Orient { get; init; } = true;
```

On `PreparedMesh`, as a trailing optional parameter so nothing already constructing one breaks:

```csharp
    string? LaidDown = null)
```

- [ ] **Step 5: Apply it between the clearance and the bed**

In `MeshPipeline.Prepare`, replace the three transform lines with:

```csharp
        // Millimetres first, because a clearance is stated in millimetres and has to be applied
        // to a shape that is already measured in them.
        var upright = StandUp(repaired, o);
        var clearance = ClearanceOffset.Apply(upright, o.ClearanceMillimetres, quality);

        // Then laid down, before it is dropped: the bed is found again afterwards, so a rolled
        // part sits on the bed rather than hovering over where its old underside used to be.
        var kind = o.Orient ? PartKinds.FromTitle(part.Title) : PartKind.Unknown;
        var laid = Orientation.For(kind) is { } turn
            ? clearance.Mesh.Transformed(turn)
            : clearance.Mesh;

        var placed = SitOnBed(laid, o);
```

and add `Orientation.Name(kind)` as the final argument of the `new PreparedMesh(...)` that follows.

- [ ] **Step 6: Run the whole suite**

```
dotnet test Lego2STL.slnx
```

Expected: PASS. If a snapshot test over a written `.stl` fails, an axle in it has moved — which is
the intended change; re-record that snapshot and say so in the commit.

- [ ] **Step 7: Commit**

```bash
git add -A
git commit -m "feat: each part is laid on the bed the way its kind is printed"
```

---

### Task 5: The run records how each part was laid down

**Files:**
- Modify: `src/Lego2STL.Core/Run/RunManifest.cs` (`ManifestPart`, and the projection in `Part`)
- Modify: `src/Lego2STL.Core/Run/RunDocument.cs` (`RunDocumentPart`, and both projections)
- Test: `tests/Lego2STL.Tests/Run/RunManifestTests.cs`

**Interfaces:**
- Consumes: `PreparedMesh.LaidDown` (Task 4).
- Produces: `ManifestPart.LaidDown` and `RunDocumentPart.LaidDown`, both `string?`, both trailing
  and optional so a record written before this still opens.

- [ ] **Step 1: Write the failing test**

Add to `tests/Lego2STL.Tests/Run/RunManifestTests.cs`:

```csharp
    /// <summary>
    /// How a part was laid on the bed is recorded, like every other decision a run makes.
    /// </summary>
    /// <remarks>
    /// Recorded so it can be disagreed with. It is also the only way the set of parts no rule
    /// matched can be read off a real run, which is how the table of rules is meant to grow.
    /// </remarks>
    [Fact]
    public async Task How_each_part_was_laid_on_the_bed_is_recorded()
    {
        var layout = ARunFolder();
        var outcome = APretendRun.Complete(layout);

        // A shape that was turned, so the test proves the value travels rather than proving that
        // two lists of nulls match.
        var turned = outcome.Shapes[0] with { LaidDown = "Axle" };
        var run = outcome with { Shapes = [turned, .. outcome.Shapes.Skip(1)] };

        var manifest = RunManifest.From(run, APretendRun.Started, APretendRun.Finished, null);

        manifest.Parts.Should().Contain(p => p.LaidDown == "Axle");

        await RunManifest.WriteAsync(layout, manifest);
        var (read, state) = RunManifest.Read(layout.ManifestPath);

        state.Should().Be(ManifestState.Present);
        read!.Parts.Should().Contain(p => p.LaidDown == "Axle");
        RunDocument.From(read, layout).Parts.Should().Contain(p => p.LaidDown == "Axle");
    }
```

- [ ] **Step 2: Run it and watch it fail**

```
dotnet test tests/Lego2STL.Tests/Lego2STL.Tests.csproj -f net10.0-windows10.0.19041.0 --filter "FullyQualifiedName~laid_on_the_bed_is_recorded"
```

Expected: FAIL to compile — `LaidDown` is not on `ManifestPart`.

- [ ] **Step 3: Carry it through both records**

On `ManifestPart`, after `string? Printability = null`:

```csharp
    string? LaidDown = null);
```

In `RunManifest.Part(...)`, pass the shape's `LaidDown` through. On `RunDocumentPart`, add the same
trailing parameter, and pass `part.LaidDown` in the projection in `RunDocument.From`. The projection
in `WithoutManifest` leaves it at its default: a folder from before this build recorded nothing.

- [ ] **Step 4: Run the whole suite**

```
dotnet test Lego2STL.slnx
```

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat: a run records how each part was laid on the bed"
```

---

### Task 6: The process preset

**Files:**
- Create: `src/Lego2STL.Core/Plates/ProcessPreset.cs`
- Test: `tests/Lego2STL.Tests/Plates/ProcessPresetTests.cs` (create)

**Interfaces:**
- Consumes: the printer name as `RunSettings.Printer` holds it.
- Produces: `static class ProcessPreset` with `For(string printer) → string?` giving the JSON, null
  when the printer has no known base, and `BaseFor(string printer) → string?`.

- [ ] **Step 1: Verify the setting keys against a preset the slicer itself wrote**

**Do this before writing any key into code.** In Bambu Studio: pick the A1 with a 0.4 mm nozzle,
select `0.16mm Optimal`, turn supports off, set the brim to auto, set elephant-foot compensation to
0.15, outer wall speed to 35, small perimeter speed to 25, walls to 3, top and bottom shell layers
to 5, infill to 15% gyroid. Save it as a user preset. Then read what it wrote:

```
ls "$APPDATA/BambuStudio/user"/*/process/
```

The OTA presets carry only deltas, so a key's absence from them proves nothing, and a guessed key
name produces a file that imports with settings silently missing. Copy the exact key names and the
exact value spellings out of that file and use them in Step 3. These were confirmed on 2026-08-31
and are expected to hold: `elefant_foot_compensation` (note the spelling — one `l`),
`outer_wall_speed`, `small_perimeter_speed`, `top_shell_layers`, `bottom_shell_layers`. These were
**not** confirmed and are what this step is for: `enable_support`, `brim_type`, `wall_loops`,
`sparse_infill_density`, `sparse_infill_pattern`.

Note which keys the file writes as an **array of one string** — on the A1 the speeds are
`["35"]`, not `"35"` — because the writer has to match.

- [ ] **Step 2: Write the failing tests**

Create `tests/Lego2STL.Tests/Plates/ProcessPresetTests.cs`:

```csharp
using System.Text.Json;
using FluentAssertions;
using Lego2STL.Core.Plates;

namespace Lego2STL.Tests.Plates;

/// <summary>
/// The slicer settings that travel beside a plate.
/// </summary>
/// <remarks>
/// Thin on purpose. A preset that inherits from the printer's own base and states only the
/// differences stays small enough to read and cannot contradict the base underneath it; a copied
/// profile would go stale at the next release of the slicer.
/// </remarks>
public sealed class ProcessPresetTests
{
    private static JsonElement Parsed(string printer) =>
        JsonDocument.Parse(ProcessPreset.For(printer)!).RootElement;

    [Theory]
    [InlineData("A1", "0.16mm Optimal @BBL A1")]
    [InlineData("A1mini", "0.16mm Optimal @BBL A1M")]
    [InlineData("P1P", "0.16mm Optimal @BBL P1P")]
    [InlineData("X1C", "0.16mm Optimal @BBL X1C")]
    [InlineData("H2D", "0.16mm Standard @BBL H2D")]
    public void Each_printer_inherits_from_its_own_base(string printer, string expected) =>
        Parsed(printer).GetProperty("inherits").GetString().Should().Be(expected);

    /// <summary>
    /// The P1S has no profiles of its own and uses the P1P's.
    /// </summary>
    /// <remarks>
    /// A substitution rather than a match, which is why the instruction sheet names it as one.
    /// </remarks>
    [Fact]
    public void The_P1S_borrows_the_P1P_profile()
    {
        Parsed("P1S").GetProperty("inherits").GetString().Should().Be("0.16mm Optimal @BBL P1P");
        ProcessPreset.BorrowedFrom("P1S").Should().Be("P1P");
    }

    /// <summary>
    /// The A1 mini has profiles of its own; they are simply named differently.
    /// </summary>
    /// <remarks>
    /// The case that rules out inferring a borrowing from whether the base mentions the printer:
    /// the A1 mini's own base is named A1M, and calling that a substitution would be a lie on the
    /// sheet.
    /// </remarks>
    [Fact]
    public void The_A1_mini_borrows_nothing() =>
        ProcessPreset.BorrowedFrom("A1mini").Should().BeNull();

    [Fact]
    public void A_printer_with_no_known_base_gets_no_file() =>
        ProcessPreset.For("some future machine").Should().BeNull();

    /// <summary>Every value is a string, and the speeds are arrays of one. That is the format.</summary>
    [Fact]
    public void Every_value_is_written_the_way_the_slicer_writes_them()
    {
        var preset = Parsed("A1");

        preset.GetProperty("type").GetString().Should().Be("process");
        preset.GetProperty("from").GetString().Should().Be("User");
        preset.GetProperty("elefant_foot_compensation").ValueKind.Should().Be(JsonValueKind.String);
        preset.GetProperty("outer_wall_speed").ValueKind.Should().Be(JsonValueKind.Array);
        preset.GetProperty("outer_wall_speed")[0].ValueKind.Should().Be(JsonValueKind.String);
    }

    /// <summary>
    /// The layer height is not asserted, because the base already is one.
    /// </summary>
    /// <remarks>
    /// Choosing a 0.16 mm base and then also writing 0.16 mm would be two places to keep in step,
    /// and the second one would eventually be the wrong one.
    /// </remarks>
    [Fact]
    public void The_layer_height_is_inherited_rather_than_repeated() =>
        Parsed("A1").TryGetProperty("layer_height", out _).Should().BeFalse();

    /// <summary>Nothing about the spool, because the tool has never seen the spool.</summary>
    [Theory]
    [InlineData("nozzle_temperature")]
    [InlineData("hot_plate_temp")]
    [InlineData("filament_max_volumetric_speed")]
    public void Nothing_about_the_filament_is_asserted(string key) =>
        Parsed("A1").TryGetProperty(key, out _).Should().BeFalse();
}
```

- [ ] **Step 3: Run them and watch them fail, then write it**

```
dotnet test tests/Lego2STL.Tests/Lego2STL.Tests.csproj -f net10.0-windows10.0.19041.0 --filter "FullyQualifiedName~ProcessPreset"
```

Expected: FAIL to compile. Then create `src/Lego2STL.Core/Plates/ProcessPreset.cs`:

```csharp
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Lego2STL.Core.Plates;

/// <summary>
/// The slicer settings that go beside a plate, as a preset the slicer can import.
/// </summary>
/// <remarks>
/// <para>
/// A preset and not a project file. The 3MF stays a plate any reader understands: writing a
/// slicer's own project format into it would trade away the one thing this tool's 3MF was written
/// for, which is that it depends on no library and so has nothing to go stale.
/// </para>
/// <para>
/// It asserts only what this tool knows. It knows the bed, because it packs onto one, and it knows
/// what it built - small interlocking parts whose accuracy matters more than their speed, laid
/// down so that no support is needed. It does not know the nozzle, the filament, the spool or the
/// room, so it says nothing about any of them; those are in the sheet beside it, as advice.
/// </para>
/// </remarks>
public static class ProcessPreset
{
    /// <summary>
    /// The base each printer's preset inherits from, as the slicer names them.
    /// </summary>
    /// <remarks>
    /// Read off a real installation. Two things here are not guessable: the family name is not
    /// uniform - the A1, P1 and X1 lines call the 0.16 mm profile Optimal while the H2 line calls
    /// it Standard - and the P1S has no profiles of its own at all and borrows the P1P's. The
    /// names are also per nozzle; these are the default 0.4 mm ones.
    /// </remarks>
    private static readonly Dictionary<string, string> Bases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["A1"] = "0.16mm Optimal @BBL A1",
        ["A1mini"] = "0.16mm Optimal @BBL A1M",
        ["P1P"] = "0.16mm Optimal @BBL P1P",
        ["P1S"] = "0.16mm Optimal @BBL P1P",
        ["X1C"] = "0.16mm Optimal @BBL X1C",
        ["H2D"] = "0.16mm Standard @BBL H2D",
    };

    /// <summary>
    /// Printers with no profiles of their own, and whose they use instead.
    /// </summary>
    /// <remarks>
    /// Declared rather than worked out from the names. Inferring it - "the base does not mention
    /// this printer, so it must be borrowed" - gets the A1 mini wrong, because its own profiles are
    /// named A1M.
    /// </remarks>
    private static readonly Dictionary<string, string> Borrowings = new(StringComparer.OrdinalIgnoreCase)
    {
        ["P1S"] = "P1P",
    };

    /// <summary>Whose profile this printer borrows, or null when it has its own.</summary>
    public static string? BorrowedFrom(string? printer) =>
        printer is not null && Borrowings.TryGetValue(printer, out var lender) ? lender : null;

    /// <summary>The base this printer's preset sits on, or null when there is none to sit on.</summary>
    public static string? BaseFor(string? printer) =>
        printer is not null && Bases.TryGetValue(printer.Replace(" ", string.Empty, StringComparison.Ordinal), out var name)
            ? name
            : null;

    /// <summary>
    /// The preset, or null for a printer with no known base.
    /// </summary>
    /// <remarks>
    /// Null rather than a guess: a preset whose inherited name does not exist fails to import, and
    /// a failed import looks like a broken tool where an absent file only looks like a quiet one.
    /// </remarks>
    public static string? For(string? printer)
    {
        if (BaseFor(printer) is not { } inherited)
        {
            return null;
        }

        var preset = new JsonObject
        {
            ["type"] = "process",
            ["name"] = $"Lego2STL 0.16mm @BBL {printer}",
            ["from"] = "User",
            ["inherits"] = inherited,

            // Every value is a string and the per-extruder ones are arrays of one, which is how
            // the slicer writes its own.
            ["enable_support"] = "0",
            ["brim_type"] = "auto_brim",
            ["elefant_foot_compensation"] = "0.15",
            ["wall_loops"] = "3",
            ["top_shell_layers"] = "5",
            ["bottom_shell_layers"] = "5",
            ["sparse_infill_density"] = "15%",
            ["sparse_infill_pattern"] = "gyroid",
            ["outer_wall_speed"] = new JsonArray("35"),
            ["small_perimeter_speed"] = new JsonArray("25"),
        };

        return preset.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }
}
```

Replace any key whose real name Step 1 showed to be different.

- [ ] **Step 4: Run the tests**

```
dotnet test tests/Lego2STL.Tests/Lego2STL.Tests.csproj -f net10.0-windows10.0.19041.0 --filter "FullyQualifiedName~ProcessPreset"
```

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Lego2STL.Core/Plates/ProcessPreset.cs tests/Lego2STL.Tests/Plates/ProcessPresetTests.cs
git commit -m "feat: a plate comes with a slicer preset that turns supports off"
```

---

### Task 7: The instruction sheet

**Files:**
- Create: `src/Lego2STL.Core/Plates/PrintNotes.cs`
- Modify: `src/Lego2STL.Core/Text/TextKey.cs`, `Strings.English.cs`, `Strings.Italian.cs`
- Test: `tests/Lego2STL.Tests/Plates/PrintNotesTests.cs` (create)

**Interfaces:**
- Consumes: `ProcessPreset.BaseFor` (Task 6), `Strings`.
- Produces: `static class PrintNotes` with `Write(string printer, Strings words) → string`.

The sheet carries what the preset declines to: temperatures, fan, all the speeds, maximum
volumetric speed, and the calibration sequence. It is the primary deliverable — a text file is
correct for ever, while a preset depends on names that may change — so it says everything, including
what the preset also says.

- [ ] **Step 1: Write the failing tests**

Create `tests/Lego2STL.Tests/Plates/PrintNotesTests.cs`:

```csharp
using FluentAssertions;
using Lego2STL.Core.Plates;
using Lego2STL.Core.Text;

namespace Lego2STL.Tests.Plates;

/// <summary>
/// The sheet that goes beside the plates.
/// </summary>
/// <remarks>
/// The same reasoning as the note the calibration command already writes: by the time a folder of
/// files is printed, the command line that made them is long gone. This one carries the settings
/// the preset deliberately does not assert, so that a person has them even when the preset cannot
/// be written at all.
/// </remarks>
public sealed class PrintNotesTests
{
    [Theory]
    [InlineData(DisplayLanguage.English)]
    [InlineData(DisplayLanguage.Italian)]
    public void The_sheet_is_written_in_the_language_of_the_run(DisplayLanguage language)
    {
        var sheet = PrintNotes.Write("A1", Strings.For(language));

        sheet.Should().NotBeNullOrWhiteSpace();
        sheet.Should().Contain(Strings.For(language)[TextKey.PrintNotesTitle]);
    }

    /// <summary>Everything the preset will not assert has to be here, or it is nowhere.</summary>
    [Theory]
    [InlineData("215")]
    [InlineData("55")]
    [InlineData("0.16")]
    public void The_settings_the_preset_declines_to_assert_are_in_the_sheet(string value) =>
        PrintNotes.Write("A1", Strings.English).Should().Contain(value);

    /// <summary>
    /// A borrowed profile is named as borrowed.
    /// </summary>
    /// <remarks>
    /// The P1S has no profiles of its own and uses the P1P's. Someone reading "P1P" on a sheet
    /// they asked for about a P1S should find out here rather than wonder.
    /// </remarks>
    [Fact]
    public void A_printer_that_borrows_another_profile_says_so() =>
        PrintNotes.Write("P1S", Strings.English).Should().Contain("P1P");

    /// <summary>
    /// The sheet says which nozzle the preset is for.
    /// </summary>
    /// <remarks>
    /// The base preset names are per nozzle - "0.16mm Optimal @BBL A1 0.2 nozzle" exists beside
    /// the plain one - and this tool does not know which nozzle is fitted, so it targets the
    /// default 0.4 mm. Someone running a 0.2 mm nozzle has to be told the preset is not theirs.
    /// </remarks>
    [Fact]
    public void The_sheet_says_which_nozzle_the_preset_is_for() =>
        PrintNotes.Write("A1", Strings.English).Should().Contain("0.4");

    /// <summary>When there is no preset, the sheet is all there is, and it still works.</summary>
    [Fact]
    public void A_printer_with_no_preset_still_gets_a_sheet()
    {
        var sheet = PrintNotes.Write("some future machine", Strings.English);

        sheet.Should().NotBeNullOrWhiteSpace();
        sheet.Should().Contain("215", "the settings are the point, and they do not depend on a preset");
    }
}
```

- [ ] **Step 2: Run them and watch them fail**

```
dotnet test tests/Lego2STL.Tests/Lego2STL.Tests.csproj -f net10.0-windows10.0.19041.0 --filter "FullyQualifiedName~PrintNotes"
```

Expected: FAIL to compile.

- [ ] **Step 3: Add the wording**

In `TextKey.cs`:

```csharp
    /// <summary>The heading of the sheet that goes beside the plates.</summary>
    PrintNotesTitle,

    /// <summary>Says the preset is a starting point and calibration beats it.</summary>
    PrintNotesStartingPoint,

    /// <summary>Tells the reader to import the preset file beside the plates.</summary>
    PrintNotesImport,

    /// <summary>Says this printer has no profiles of its own and whose it borrows.</summary>
    PrintNotesBorrowedProfile,

    /// <summary>Says no preset could be written for this printer.</summary>
    PrintNotesNoPreset,

    /// <summary>Says which nozzle the preset is for.</summary>
    PrintNotesNozzle,

    /// <summary>The heading over the table of settings.</summary>
    PrintNotesSettings,

    /// <summary>The heading over the calibration sequence.</summary>
    PrintNotesCalibration,

    /// <summary>The calibration sequence itself.</summary>
    PrintNotesCalibrationSteps,
```

In `Strings.English.cs`:

```csharp
            [TextKey.PrintNotesTitle] = "Printing these plates",
            [TextKey.PrintNotesStartingPoint] =
                "These are starting values, not answers. The moment you have calibrated your own "
                + "machine and your own spool, your figures beat every number here.",
            [TextKey.PrintNotesImport] =
                "Import {0} beside this file as a process preset, and select it before slicing. "
                + "It turns supports off, which the parts on these plates were laid down to make "
                + "unnecessary.",
            [TextKey.PrintNotesBorrowedProfile] =
                "The {0} has no profiles of its own, so this preset inherits the {1}'s. That is a "
                + "substitution and not a match; check the bed size before you print.",
            [TextKey.PrintNotesNoPreset] =
                "No preset was written: this tool does not know which profile a {0} inherits from. "
                + "Set the values below by hand.",
            [TextKey.PrintNotesNozzle] =
                "The preset is for the default 0.4 mm nozzle. With any other nozzle it is the "
                + "wrong preset: choose the profile in the slicer whose name ends in your nozzle "
                + "size, and set the values below on top of it.",
            [TextKey.PrintNotesSettings] = "Settings",
            [TextKey.PrintNotesCalibration] = "Calibrate first",
            [TextKey.PrintNotesCalibrationSteps] =
                "Wash the plate with warm water and washing-up liquid and dry it without touching "
                + "the printing surface. Then, for each spool: Calibration > Flow Dynamics, then "
                + "Calibration > Flow Rate, and save the result as a preset of its own. A damp "
                + "spool ruins a print more thoroughly than any setting here can fix.",
```

In `Strings.Italian.cs`:

```csharp
            [TextKey.PrintNotesTitle] = "Stampare questi piatti",
            [TextKey.PrintNotesStartingPoint] =
                "Questi sono valori di partenza, non risposte. Dal momento in cui hai calibrato la "
                + "tua macchina e la tua bobina, i tuoi numeri battono ogni valore scritto qui.",
            [TextKey.PrintNotesImport] =
                "Importa {0}, che sta accanto a questo file, come preset di processo, e "
                + "selezionalo prima di affettare. Disattiva i supporti, che i pezzi di questi "
                + "piatti sono stati appoggiati apposta per non richiedere.",
            [TextKey.PrintNotesBorrowedProfile] =
                "La {0} non ha profili propri, quindi questo preset eredita da quelli della {1}. È "
                + "una sostituzione, non una corrispondenza: controlla la dimensione del piano "
                + "prima di stampare.",
            [TextKey.PrintNotesNoPreset] =
                "Nessun preset è stato scritto: questo strumento non sa da quale profilo erediti "
                + "una {0}. Imposta a mano i valori qui sotto.",
            [TextKey.PrintNotesNozzle] =
                "Il preset è per l'ugello predefinito da 0,4 mm. Con qualunque altro ugello è il "
                + "preset sbagliato: scegli nello slicer il profilo il cui nome finisce con la "
                + "misura del tuo ugello, e applicaci sopra i valori qui sotto.",
            [TextKey.PrintNotesSettings] = "Impostazioni",
            [TextKey.PrintNotesCalibration] = "Prima calibra",
            [TextKey.PrintNotesCalibrationSteps] =
                "Lava il piano con acqua calda e detersivo per piatti e asciugalo senza toccare la "
                + "superficie di stampa. Poi, per ogni bobina: Calibration > Flow Dynamics, quindi "
                + "Calibration > Flow Rate, e salva il risultato come preset a sé. Una bobina umida "
                + "rovina una stampa più a fondo di quanto qualunque impostazione qui possa "
                + "rimediare.",
```

- [ ] **Step 4: Write the sheet**

Create `src/Lego2STL.Core/Plates/PrintNotes.cs`:

```csharp
using System.Text;
using Lego2STL.Core.Text;

namespace Lego2STL.Core.Plates;

/// <summary>
/// The sheet that goes beside a run's plates, saying how to print them.
/// </summary>
/// <remarks>
/// <para>
/// The same reasoning as the note the calibration command writes beside its shapes: by the time a
/// folder of files is printed, the command line that made them is long gone.
/// </para>
/// <para>
/// This is the primary deliverable and the preset beside it is the convenience. A text file is
/// still correct in five years, while a preset depends on names the slicer may rename; so the
/// sheet carries every setting, including the ones the preset also carries, and a printer with no
/// preset still gets everything it needs.
/// </para>
/// </remarks>
public static class PrintNotes
{
    /// <summary>
    /// The starting profile.
    /// </summary>
    /// <remarks>
    /// Literal values, because these are advice rather than anything derived: they describe a
    /// spool this tool has never seen. The setting names are left in the slicer's own English
    /// because they are what the reader is hunting for on screen.
    /// </remarks>
    private static readonly (string Setting, string Value)[] Profile =
    [
        ("Nozzle temperature", "215 C"),
        ("Nozzle temperature, first layer", "220 C"),
        ("Bed temperature", "55 C"),
        ("Bed temperature, first layer", "60 C"),
        ("Part cooling fan", "0% first layer, 100% from the third"),
        ("Layer height", "0.16 mm"),
        ("First layer height", "0.20 mm"),
        ("First layer speed", "20 mm/s"),
        ("Outer wall speed", "35 mm/s"),
        ("Inner wall speed", "50 mm/s"),
        ("Top surface speed", "30 mm/s"),
        ("Small perimeter speed", "25 mm/s"),
        ("Sparse infill speed", "60 mm/s"),
        ("Max volumetric speed", "10 mm3/s"),
        ("Walls", "3"),
        ("Top shell layers", "5"),
        ("Bottom shell layers", "5"),
        ("Sparse infill", "15% gyroid"),
        ("Elephant foot compensation", "0.15 mm"),
        ("Brim", "auto"),
        ("Supports", "off"),
    ];

    /// <summary>The sheet, for this printer, in these words.</summary>
    public static string Write(string? printer, Strings words)
    {
        ArgumentNullException.ThrowIfNull(words);

        var name = string.IsNullOrWhiteSpace(printer) ? "?" : printer.Trim();
        var sheet = new StringBuilder();

        var title = words[TextKey.PrintNotesTitle];
        sheet.AppendLine(title).AppendLine(new string('=', title.Length)).AppendLine();
        sheet.AppendLine(words[TextKey.PrintNotesStartingPoint]).AppendLine();

        if (ProcessPreset.BaseFor(name) is not null)
        {
            sheet.AppendLine(words.Format(TextKey.PrintNotesImport, "Lego2STL.json"));
            sheet.AppendLine(words[TextKey.PrintNotesNozzle]);

            if (ProcessPreset.BorrowedFrom(name) is { } lender)
            {
                sheet.AppendLine(words.Format(TextKey.PrintNotesBorrowedProfile, name, lender));
            }
        }
        else
        {
            sheet.AppendLine(words.Format(TextKey.PrintNotesNoPreset, name));
        }

        sheet.AppendLine().AppendLine(words[TextKey.PrintNotesSettings]).AppendLine();

        foreach (var (setting, value) in Profile)
        {
            sheet.Append("  ").Append(setting.PadRight(34)).AppendLine(value);
        }

        sheet.AppendLine().AppendLine(words[TextKey.PrintNotesCalibration]).AppendLine();
        sheet.AppendLine(words[TextKey.PrintNotesCalibrationSteps]);

        return sheet.ToString();
    }
}
```

The degree sign and the cubed sign are left out of the values on purpose: this file is written as
plain text and read in whatever the reader's terminal or editor guesses, and a mangled `°` in the
one line someone is squinting at is worse than its absence.

- [ ] **Step 5: Run the whole suite**

```
dotnet test Lego2STL.slnx
```

Expected: PASS, including the completeness check over every `TextKey` in both languages.

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "feat: a sheet beside the plates says how to print them"
```

---

### Task 8: A run that writes plates writes both beside them

**Files:**
- Modify: `src/Lego2STL.Core/Run/RunLayout.cs`
- Modify: `src/Lego2STL.Core/Pipeline/PipelineRunner.cs:669-685`
- Test: `tests/Lego2STL.Tests/Pipeline/PrintSettingsTests.cs` (create)

**Interfaces:**
- Consumes: `ProcessPreset.For` (Task 6), `PrintNotes.Write` (Task 7).
- Produces: `RunLayout.PresetPath` and `RunLayout.PrintNotesPath`, both inside the plate directory.

- [ ] **Step 1: Write the failing tests**

Create `tests/Lego2STL.Tests/Pipeline/PrintSettingsTests.cs`:

```csharp
using FluentAssertions;
using Lego2STL.Core.Run;

namespace Lego2STL.Tests.Pipeline;

/// <summary>
/// The two files that go beside a run's plates.
/// </summary>
/// <remarks>
/// They live with the plates because they are about printing plates. The catalogue's scan of that
/// folder matches only *.3mf, so neighbours there are harmless.
/// </remarks>
public sealed class PrintSettingsTests
{
    [Fact]
    public void The_settings_live_beside_the_plates()
    {
        var layout = RunLayout.At(APretendRun.TempFolder("settings"));

        Path.GetDirectoryName(layout.PresetPath).Should().Be(layout.PlateDirectory);
        Path.GetDirectoryName(layout.PrintNotesPath).Should().Be(layout.PlateDirectory);
        Path.GetExtension(layout.PresetPath).Should().Be(".json");
    }
}
```

- [ ] **Step 2: Run it and watch it fail**

```
dotnet test tests/Lego2STL.Tests/Lego2STL.Tests.csproj -f net10.0-windows10.0.19041.0 --filter "FullyQualifiedName~PrintSettings"
```

Expected: FAIL to compile.

- [ ] **Step 3: Add the two paths**

In `RunLayout.cs`, beside `PlateDirectory`:

```csharp
    /// <summary>The slicer preset that goes with the plates, when one could be written.</summary>
    public string PresetPath => Path.Combine(PlateDirectory, "Lego2STL.json");

    /// <summary>How to print the plates, for a person.</summary>
    public string PrintNotesPath => Path.Combine(PlateDirectory, "how-to-print.txt");
```

- [ ] **Step 4: Write them where the plates are written**

In `PipelineRunner`, immediately after the `PlateBuilder.WriteAsync` call and before the
`plates.Skipped` loop:

```csharp
        // Written here rather than inside the plate builder: the builder's job is arranging
        // shapes, and this is about the machine that will print them.
        if (ProcessPreset.For(settings.Printer) is { } preset)
        {
            await File.WriteAllTextAsync(layout.PresetPath, preset, cancellationToken)
                .ConfigureAwait(false);
        }

        await File.WriteAllTextAsync(
                layout.PrintNotesPath,
                PrintNotes.Write(settings.Printer, words),
                cancellationToken)
            .ConfigureAwait(false);
```

Both are inside the method that only runs when plates were asked for, so `--no-plates` writes
neither without any extra condition. Confirm that is true of the method being edited before relying
on it; if it is not, guard on `settings.WantsPlates`.

- [ ] **Step 5: Say so, once**

Add a `TextKey.MsgWrotePrintSettings` in both languages — English *"Wrote how to print them to
{0}"*, Italian *"Come stamparli scritto in {0}"* — and log it after `MsgWrotePlates`.

- [ ] **Step 6: Run the whole suite**

```
dotnet test Lego2STL.slnx
```

Expected: PASS.

- [ ] **Step 7: Do it for real**

```
dotnet run --project src/Lego2STL.Cli -- build 6324712/6324712.csv --printer A1 --lang it
```

Then, by hand, and this is the part no test reaches:

1. Open `6324712/3mf/how-to-print.txt` and read it as a person who has never used this tool.
2. Import `6324712/3mf/Lego2STL.json` into Bambu Studio. It must appear as a process preset and
   select without complaint. If it fails to import, a key name from Task 6 Step 1 is wrong.
3. Open one plate with the preset selected and check that supports are off.
4. Look at an axle on the plate: it should be resting on one arm of its cross, not flat.

- [ ] **Step 8: Commit**

```bash
git add -A
git commit -m "feat: a run writes the settings to print its plates with"
```

---

## Notes for whoever executes this

- **Task 3 is a gate, not a formality.** If rolling a real axle does not reduce its overhang for all
  six tested, the axle rule does not ship, Tasks 4 and 5 keep only the confirming rules, and the
  spec is corrected. A rule that is wrong about the one thing it was built for is worse than no
  rule.
- **Task 6 Step 1 is also a gate.** Guessing a Bambu setting key produces a file that imports
  cleanly with the setting silently missing, which is the worst possible failure: it looks like it
  worked. Verify against a preset the slicer wrote before committing any key name.
- The tests in Task 4 that assert **nothing moved** are the most valuable ones in this plan. They
  are what stops the orientation table quietly becoming an optimiser in six months.
- Record `PHASE:AB WAVE:<n> STATUS:complete TS:<ISO-8601-UTC>` in `PROGRESS.md` after each task, and
  `PHASE:AB WAVE:0 STATUS:complete` when all eight are done.
