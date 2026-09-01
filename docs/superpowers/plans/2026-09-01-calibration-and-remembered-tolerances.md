# Calibration, And Remembering Its Answer — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A calibration you print as one plate instead of arranging by hand, and an answer the tool
keeps instead of one you retype for ever.

**Architecture:** Three moving parts. `PlateBuilder` is split at the seam it already has, so that
"named meshes become plate files" is reachable without a parts list — which is what lets a
calibration plate carry the same part six times at six clearances. A tolerance store in Core holds
named clearances where both the command line and the window can read it, since `UserSettings` is
the window's own file. And one pure resolver decides which clearance a build uses, so the two front
ends cannot disagree.

**Tech Stack:** C# / .NET 10, xUnit + FluentAssertions, System.Text.Json, System.Numerics, Avalonia
with CommunityToolkit.Mvvm.

**Spec:** `docs/superpowers/specs/2026-09-01-calibration-and-remembered-tolerances-design.md`

## Global Constraints

- Build with `dotnet build Lego2STL.slnx -c Debug`. Test with `dotnet test Lego2STL.slnx`.
- The suite runs on the Windows target only — `net10.0-windows10.0.19041.0` — because reading a
  document needs the recogniser that is part of Windows. Filter a single test with
  `dotnet test tests/Lego2STL.Tests/Lego2STL.Tests.csproj -f net10.0-windows10.0.19041.0 --filter "FullyQualifiedName~<name>"`.
- **Do not run a build inside `6324712/`.** Its `run.json` and `run.log` are committed as a
  reference and a smaller run overwrites them. Every command in this plan that produces output
  passes `--output-dir`; keep that true of anything you try by hand.
- Every user-facing string goes through `TextKey` and is added to **both** `Strings.English.cs` and
  `Strings.Italian.cs`. The suite walks every key in both languages and fails on a gap.
- Source files are CRLF. They carry a UTF-8 byte-order mark **only when they contain a non-ASCII
  character** — that is the repository's actual convention, 34 files with, 166 without. A pure
  ASCII file gets no BOM; `Strings.Italian.cs` has one.
- Code comments and CHANGELOG entries: **one sentence each**. Test comments are exempt.
- Commit messages: `<type>: <description>`, describing observable behaviour, never internal class
  or method names.
- Files stay under 800 lines; functions under 50.
- **Which test project.** `Lego2STL.Tests` references `Lego2STL.Core` and nothing else, and
  `Lego2STL.Cli`'s `InternalsVisibleTo` names only `Lego2STL.UiTests`. So anything testing Core goes
  in `Lego2STL.Tests`, and **anything that drives the command line's own declaration goes in
  `Lego2STL.UiTests`**, where `OptionParityTests` already does exactly that. Do not add a project
  reference to move them; the split is deliberate.
- `Lego2STL.UiTests` points `LEGO2STL_SETTINGS_DIR` at one temporary folder for the whole assembly
  through `Isolation.cs`, so tolerance presets there are shared between tests in that project.
  Every test in it that touches the store must clear it first. `Lego2STL.Tests` instead uses the
  `AppDataFolder` helper with `[Collection(AppDataFolder.Name)]`, one folder per test.
- **`PlateBuilderTests` must pass unchanged.** If Task 1 requires editing one existing assertion,
  the extraction changed behaviour and is wrong — stop and re-do it.
- **Nothing here invents a clearance.** The command's refusal to offer a default survives: a build
  with no preset named and none preferred runs at true size exactly as it does today.
- **No clearance value is ever embossed into geometry.** Position on the plate plus the sheet says
  which piece is which.
- **A tolerance preset lives only in `tolerances.json`.** Do not add tolerance fields to
  `UserSettings`; the command line cannot see that file.
- After each task append one line to `PROGRESS.md`:
  `PHASE:C WAVE:<n> STATUS:complete TS:<ISO-8601-UTC>`, and `PHASE:C WAVE:0` when all twelve are
  done.

---

### Task 1: Named meshes become plate files, without a parts list

**Files:**
- Create: `src/Lego2STL.Core/Plates/PlateWriter.cs`
- Modify: `src/Lego2STL.Core/Plates/PlateBuilder.cs:69-158` (the body of `WriteAsync`)
- Test: `tests/Lego2STL.Tests/Plates/PlateWriterTests.cs` (create)

**Interfaces:**
- Consumes: `ShelfPacker.Pack`, `ThreeMfWriter.WriteFileAsync`, `PlateFileName.For`,
  `PackableItem`, `PackedPlate`, `PlateContents`, `PlateObject`, `SkippedPart` — all unchanged.
- Produces:
  - `sealed record PlateItem(string Label, IndexedMesh Mesh, int Quantity)`
  - `sealed record WrittenPlate(string FileName, int Number, int PieceCount, string Footprint)`
  - `sealed record PlateWriteResult(IReadOnlyList<WrittenPlate> Plates, IReadOnlyList<SkippedPart> Skipped)`
  - `static Task<PlateWriteResult> PlateWriter.WritePlatesAsync(IReadOnlyList<PlateItem> items, string fileStem, string colorName, Rgb24 rgb, string directory, PackingOptions? options = null, CancellationToken cancellationToken = default)`

`PlateBuilder.WriteAsync` keeps its exact public signature and becomes the parts-list-and-colour
layer over this. The label on a `PackableItem` was never checked against a real part number — this
task only makes that reachable.

- [ ] **Step 1: Write the failing test**

Create `tests/Lego2STL.Tests/Plates/PlateWriterTests.cs`:

```csharp
using System.Numerics;
using FluentAssertions;
using Lego2STL.Core.Colors;
using Lego2STL.Core.Geometry;
using Lego2STL.Core.Plates;

namespace Lego2STL.Tests.Plates;

/// <summary>
/// Arranging named shapes onto plate files, with no parts list involved.
/// </summary>
/// <remarks>
/// This is the half of the plate stage that a calibration needs and a parts list does not. A
/// calibration plate carries the same part six times at six clearances, which a dictionary keyed
/// by part number cannot express, so the packing had to become reachable on its own.
/// </remarks>
public sealed class PlateWriterTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "lego2stl-platewriter-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    /// <summary>A tetrahedron: the smallest closed shape, so a valid mesh to place.</summary>
    private static IndexedMesh Tetrahedron(float size = 10f) =>
        new(
            [
                new Vector3(0, 0, 0),
                new Vector3(size, 0, 0),
                new Vector3(0, size, 0),
                new Vector3(0, 0, size),
            ],
            [
                new IndexedTriangle(0, 2, 1),
                new IndexedTriangle(0, 1, 3),
                new IndexedTriangle(0, 3, 2),
                new IndexedTriangle(1, 2, 3),
            ]);

    private static Rgb24 Grey => Rgb24.Parse("#C8C8C8");

    /// <summary>
    /// The labels are not part numbers, and nothing minds.
    /// </summary>
    /// <remarks>
    /// The point of the whole extraction. Three labels naming the same part at three clearances
    /// are three distinct things on the plate, which is exactly what a dictionary keyed by part
    /// number could not say.
    /// </remarks>
    [Fact]
    public async Task Labels_that_are_not_part_numbers_are_each_placed()
    {
        var items = new List<PlateItem>
        {
            new("3705-0.00mm", Tetrahedron(), 1),
            new("3705-0.05mm", Tetrahedron(), 1),
            new("3705-0.10mm", Tetrahedron(), 1),
        };

        var result = await PlateWriter.WritePlatesAsync(
            items, "calibration", "Calibration", Grey, _directory);

        result.Skipped.Should().BeEmpty();
        result.Plates.Should().ContainSingle();
        result.Plates[0].FileName.Should().Be("calibration.3mf");
        result.Plates[0].PieceCount.Should().Be(3);
        File.Exists(Path.Combine(_directory, "calibration.3mf")).Should().BeTrue();
    }

    /// <summary>A quantity puts that many copies on, as the parts-list layer has always relied on.</summary>
    [Fact]
    public async Task A_quantity_puts_that_many_copies_on()
    {
        var result = await PlateWriter.WritePlatesAsync(
            [new PlateItem("pin", Tetrahedron(), 7)], "one", "Black", Grey, _directory);

        result.Plates.Should().ContainSingle();
        result.Plates[0].PieceCount.Should().Be(7);
    }

    /// <summary>Something no bed can take is reported rather than dropped in silence.</summary>
    [Fact]
    public async Task A_shape_too_big_for_the_bed_is_reported()
    {
        var result = await PlateWriter.WritePlatesAsync(
            [new PlateItem("enormous", Tetrahedron(4000f), 1)], "one", "Black", Grey, _directory);

        result.Plates.Should().BeEmpty();
        result.Skipped.Should().ContainSingle(s => s.PartNumber == "enormous");
    }

    /// <summary>More than one plate's worth is numbered, because the file name has to differ.</summary>
    [Fact]
    public async Task More_than_one_plates_worth_is_numbered()
    {
        var many = Enumerable.Range(0, 900)
            .Select(i => new PlateItem($"item-{i:000}", Tetrahedron(), 1))
            .ToList();

        var result = await PlateWriter.WritePlatesAsync(
            many, "calibration", "Calibration", Grey, _directory);

        result.Plates.Count.Should().BeGreaterThan(1);
        result.Plates[0].FileName.Should().Be("calibration-1.3mf");
    }
}
```

- [ ] **Step 2: Run it and watch it fail**

```
dotnet test tests/Lego2STL.Tests/Lego2STL.Tests.csproj -f net10.0-windows10.0.19041.0 --filter "FullyQualifiedName~PlateWriter"
```

Expected: FAIL to compile — `PlateWriter` and `PlateItem` do not exist.

- [ ] **Step 3: Write the new file**

Create `src/Lego2STL.Core/Plates/PlateWriter.cs`:

```csharp
using System.Numerics;
using Lego2STL.Core.Colors;
using Lego2STL.Core.Geometry;

namespace Lego2STL.Core.Plates;

/// <summary>One shape to go on a plate, under whatever name the caller knows it by.</summary>
/// <param name="Label">
/// What to call it. A part number when a parts list is being plated, and something else entirely
/// when it is not - a calibration plate carries one part at six clearances, which are six labels.
/// </param>
public sealed record PlateItem(string Label, IndexedMesh Mesh, int Quantity);

/// <summary>A plate file that was written.</summary>
public sealed record WrittenPlate(string FileName, int Number, int PieceCount, string Footprint);

/// <summary>What one call produced: the files, and whatever no bed could take.</summary>
public sealed record PlateWriteResult(
    IReadOnlyList<WrittenPlate> Plates,
    IReadOnlyList<SkippedPart> Skipped);

/// <summary>
/// Arranges named shapes onto plates and writes them, all in one colour.
/// </summary>
/// <remarks>
/// <para>
/// The half of the plate stage that knows nothing about parts lists. Grouping a set by colour,
/// honouring quantities from a catalogue and naming files after a translated colour are the other
/// half, and they live in <see cref="PlateBuilder"/> on top of this.
/// </para>
/// <para>
/// Split apart because a calibration plate carries the same part several times at several
/// clearances, and the only handle the old entry point offered was a dictionary keyed by part
/// number, which cannot say that. Nothing below ever checked that a label was a real part number.
/// </para>
/// </remarks>
public static class PlateWriter
{
    /// <param name="fileStem">What the files are named after, before any plate number.</param>
    /// <param name="colorName">The colour as the caller words it, written into the plate itself.</param>
    public static async Task<PlateWriteResult> WritePlatesAsync(
        IReadOnlyList<PlateItem> items,
        string fileStem,
        string colorName,
        Rgb24 rgb,
        string directory,
        PackingOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileStem);
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);

        Directory.CreateDirectory(directory);

        var meshes = new Dictionary<string, IndexedMesh>(StringComparer.Ordinal);
        var packable = new List<PackableItem>();

        foreach (var item in items)
        {
            if (item.Mesh.TriangleCount == 0)
            {
                continue;
            }

            meshes[item.Label] = item.Mesh;

            var (min, max) = item.Mesh.Bounds();
            var size = max - min;
            var one = new PackableItem(item.Label, new Vector2(size.X, size.Y), size.Z);

            for (var i = 0; i < item.Quantity; i++)
            {
                packable.Add(one);
            }
        }

        var packed = ShelfPacker.Pack(packable, options ?? new PackingOptions());
        var skipped = new List<SkippedPart>();

        foreach (var over in packed.Oversized.DistinctBy(x => x.Item.PartNumber, StringComparer.Ordinal))
        {
            skipped.Add(new SkippedPart(
                over.Item.PartNumber,
                over.Item.Footprint.X,
                over.Item.Footprint.Y,
                over.Item.Height,
                over.TooTall));
        }

        var written = new List<WrittenPlate>();

        foreach (var plate in packed.Plates)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var name = PlateFileName.For(fileStem, plate.Number, packed.Plates.Count);

            await ThreeMfWriter
                .WriteFileAsync(
                    Path.Combine(directory, name),
                    Contents(name, colorName, rgb, plate, meshes),
                    cancellationToken)
                .ConfigureAwait(false);

            written.Add(new WrittenPlate(name, plate.Number, plate.PieceCount, plate.DescribeUsed()));
        }

        return new PlateWriteResult(written, skipped);
    }

    /// <summary>
    /// One entry per distinct shape, carrying every place a copy of it sits, so that the file
    /// holds each mesh once however many copies are on the plate.
    /// </summary>
    private static PlateContents Contents(
        string name,
        string colorName,
        Rgb24 rgb,
        PackedPlate plate,
        IReadOnlyDictionary<string, IndexedMesh> meshes)
    {
        var objects = new List<PlateObject>();

        foreach (var byLabel in plate.Items.GroupBy(p => p.Item.PartNumber, StringComparer.Ordinal))
        {
            var mesh = meshes[byLabel.Key];
            var (min, _) = mesh.Bounds();

            // Placements are the near-left corner of the footprint, and a shape sits wherever
            // its own origin left it, so shift by the corner of its box to land it exactly.
            var positions = byLabel
                .Select(p => new Vector2(p.X - min.X, p.Y - min.Y))
                .ToList();

            objects.Add(new PlateObject(byLabel.Key, mesh, positions));
        }

        return new PlateContents(name, colorName, rgb, objects);
    }
}
```

- [ ] **Step 4: Make `PlateBuilder.WriteAsync` call it**

In `PlateBuilder.cs`, replace the body of the `foreach (var colorGroup in GroupByColor(list))` loop
so that it builds `PlateItem`s and delegates. The whole loop becomes:

```csharp
        foreach (var colorGroup in GroupByColor(list))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var items = new List<PlateItem>();

            foreach (var entry in colorGroup.Entries)
            {
                if (!shapesByPart.TryGetValue(entry.PartNumber, out var mesh) || mesh.TriangleCount == 0)
                {
                    // No shape for it. The build stage has already said so; not repeated here.
                    continue;
                }

                items.Add(new PlateItem(entry.PartNumber, mesh, entry.Quantity));
            }

            if (items.Count == 0)
            {
                continue;
            }

            // The colour is named in the run's language here and nowhere earlier: the file
            // name, the plate's own title and the report all come from this one wording.
            var colorName = ColorNames.For(language, colorGroup.ColorName);

            var result = await PlateWriter
                .WritePlatesAsync(items, colorName, colorName, colorGroup.Rgb, directory, o, cancellationToken)
                .ConfigureAwait(false);

            skipped.AddRange(result.Skipped);

            foreach (var plate in result.Plates)
            {
                written.Add(new BuiltPlate(
                    plate.FileName,
                    colorName,
                    colorGroup.BrickLinkColorCode,
                    colorGroup.Rgb,
                    plate.Number,
                    plate.PieceCount,
                    plate.Footprint));
            }
        }
```

Then delete `PlateBuilder`'s now-unused private `Contents` method, and remove any `using` that
becomes unused (`System.Numerics` may still be needed elsewhere in the file — let the compiler
say).

- [ ] **Step 5: Run the whole suite**

```
dotnet test Lego2STL.slnx
```

Expected: PASS, with **`PlateBuilderTests` untouched**. If any of its assertions now fail, the
extraction changed behaviour: the likely cause is the file stem, which must stay the translated
colour name so `PlateFileName.For` produces exactly the name it did before.

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "refactor: arranging shapes onto a plate no longer needs a parts list"
```

---

### Task 2: The print settings can be part of somebody else's sheet

**Files:**
- Modify: `src/Lego2STL.Core/Plates/PrintNotes.cs`
- Test: `tests/Lego2STL.Tests/Plates/PrintNotesTests.cs` (add to it)

**Interfaces:**
- Consumes: `ProcessPreset.BaseFor`, `ProcessPreset.BorrowedFrom`, `Strings` — unchanged.
- Produces: `static string PrintNotes.Settings(Strings words)` — the settings block on its own,
  heading included, with no title and no calibration section. `PrintNotes.Write` keeps its exact
  signature and now composes this.

The calibration folder already writes one sheet. Task 8 makes that sheet carry the print settings
too, and two overlapping instruction files in one folder is the confusion the note exists to
prevent — so the settings block has to be obtainable without the whole document around it.

- [ ] **Step 1: Write the failing test**

Add to `tests/Lego2STL.Tests/Plates/PrintNotesTests.cs`:

```csharp
    /// <summary>
    /// The settings block can be had on its own, for a sheet that is not this one.
    /// </summary>
    /// <remarks>
    /// A calibration folder gets a single sheet under its own name, carrying the settings and
    /// then its own map and instructions. Writing a second file called how-to-print.txt beside it
    /// would leave two overlapping documents in one folder, which is exactly what the note was
    /// written to prevent.
    /// </remarks>
    [Fact]
    public void The_settings_block_is_available_without_the_rest_of_the_sheet()
    {
        var block = PrintNotes.Settings(Strings.English);

        block.Should().Contain(Strings.English[TextKey.PrintNotesSettings]);
        block.Should().Contain("215").And.Contain("0.16");
        block.Should().NotContain(Strings.English[TextKey.PrintNotesTitle]);
        block.Should().NotContain(Strings.English[TextKey.PrintNotesCalibration]);
    }

    /// <summary>The whole sheet still contains the block, because it is made of it.</summary>
    [Fact]
    public void The_whole_sheet_contains_the_block_verbatim() =>
        PrintNotes.Write("A1", Strings.English)
            .Should().Contain(PrintNotes.Settings(Strings.English));
```

- [ ] **Step 2: Run it and watch it fail**

```
dotnet test tests/Lego2STL.Tests/Lego2STL.Tests.csproj -f net10.0-windows10.0.19041.0 --filter "FullyQualifiedName~PrintNotes"
```

Expected: FAIL to compile — `PrintNotes.Settings` does not exist.

- [ ] **Step 3: Lift the block out**

In `PrintNotes.cs`, add the public method and have `Write` use it. Replace the block that appends
the settings inside `Write`:

```csharp
        sheet.AppendLine().AppendLine(words[TextKey.PrintNotesSettings]).AppendLine();

        foreach (var (setting, value) in Profile)
        {
            sheet.Append("  ").Append(setting.PadRight(34)).AppendLine(value);
        }
```

with:

```csharp
        sheet.AppendLine().Append(Settings(words));
```

and add, after `Write`:

```csharp
    /// <summary>
    /// The starting profile on its own, for a sheet that is not this one.
    /// </summary>
    /// <remarks>
    /// A calibration folder keeps one sheet rather than two, so it composes this into a document
    /// of its own instead of getting a second file beside it.
    /// </remarks>
    public static string Settings(Strings words)
    {
        ArgumentNullException.ThrowIfNull(words);

        var block = new StringBuilder();
        block.AppendLine(words[TextKey.PrintNotesSettings]).AppendLine();

        foreach (var (setting, value) in Profile)
        {
            block.Append("  ").Append(setting.PadRight(34)).AppendLine(value);
        }

        return block.ToString();
    }
```

- [ ] **Step 4: Run the tests**

```
dotnet test tests/Lego2STL.Tests/Lego2STL.Tests.csproj -f net10.0-windows10.0.19041.0 --filter "FullyQualifiedName~PrintNotes"
```

Expected: PASS. The existing sheet tests must be unchanged; if `The_whole_sheet_contains_the_block_verbatim`
fails, the blank lines around the block moved and `Write` needs its spacing put back.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "refactor: the print settings can be carried inside another sheet"
```

---

### Task 3: A named clearance, kept

**Files:**
- Create: `src/Lego2STL.Core/Run/TolerancePresets.cs`
- Test: `tests/Lego2STL.Tests/Run/TolerancePresetsTests.cs` (create)

**Interfaces:**
- Consumes: `AppDataDirectory.File`.
- Produces:
  - `sealed record TolerancePreset(string Name, double Millimetres, bool Preferred, DateTimeOffset SavedAt)`
  - `static class TolerancePresets` with `FilePath`, `Load() → IReadOnlyList<TolerancePreset>`,
    `Save(IReadOnlyList<TolerancePreset>)`, `Remember(string name, double mm, bool preferred) → IReadOnlyList<TolerancePreset>`,
    `Prefer(string name) → IReadOnlyList<TolerancePreset>`, `Forget(string name) → IReadOnlyList<TolerancePreset>`,
    and `Find(IReadOnlyList<TolerancePreset>, string name) → TolerancePreset?`.

In Core, not in `UserSettings`: the command line does not reference `Lego2STL.Gui` and cannot see
that file.

- [ ] **Step 1: Write the failing tests**

Create `tests/Lego2STL.Tests/Run/TolerancePresetsTests.cs`:

```csharp
using FluentAssertions;
using Lego2STL.Core.Run;

namespace Lego2STL.Tests.Run;

/// <summary>
/// The clearance you measured, kept under a name you chose.
/// </summary>
/// <remarks>
/// A name rather than a composed key of printer and nozzle and material, because two spools of
/// the same material behave differently and a machine drifts. The name is what survives that; a
/// key is not.
/// </remarks>
[Collection(AppDataFolder.Name)]
public sealed class TolerancePresetsTests : IDisposable
{
    private readonly AppDataFolder _folder = new();

    public void Dispose() => _folder.Dispose();

    [Fact]
    public void A_saved_figure_comes_back()
    {
        TolerancePresets.Remember("eSUN PLA+ black - A1", 0.15, preferred: false);

        var read = TolerancePresets.Load();

        read.Should().ContainSingle();
        read[0].Name.Should().Be("eSUN PLA+ black - A1");
        read[0].Millimetres.Should().Be(0.15);
        read[0].Preferred.Should().BeFalse();
    }

    /// <summary>Losing a preference must never stop a run, so an unreadable file is no presets.</summary>
    [Fact]
    public void A_file_that_cannot_be_read_is_treated_as_none()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(TolerancePresets.FilePath)!);
        File.WriteAllText(TolerancePresets.FilePath, "{ this is not json");

        TolerancePresets.Load().Should().BeEmpty();
    }

    [Fact]
    public void No_file_at_all_is_no_presets() => TolerancePresets.Load().Should().BeEmpty();

    /// <summary>Saving a name that exists replaces it rather than making a second of it.</summary>
    [Fact]
    public void Saving_a_name_that_exists_replaces_it()
    {
        TolerancePresets.Remember("black", 0.10, preferred: false);
        TolerancePresets.Remember("black", 0.20, preferred: false);

        var read = TolerancePresets.Load();

        read.Should().ContainSingle();
        read[0].Millimetres.Should().Be(0.20);
    }

    /// <summary>
    /// At most one preset is preferred, and that is the store's own guarantee.
    /// </summary>
    /// <remarks>
    /// An invariant here rather than a convention its callers keep, because a build silently
    /// picking one of two preferred presets would apply a number nobody chose.
    /// </remarks>
    [Fact]
    public void Only_one_preset_is_ever_preferred()
    {
        TolerancePresets.Remember("first", 0.10, preferred: true);
        TolerancePresets.Remember("second", 0.20, preferred: true);

        var read = TolerancePresets.Load();

        read.Should().HaveCount(2);
        read.Where(p => p.Preferred).Should().ContainSingle(p => p.Name == "second");
    }

    [Fact]
    public void Preferring_one_unprefers_the_others()
    {
        TolerancePresets.Remember("first", 0.10, preferred: true);
        TolerancePresets.Remember("second", 0.20, preferred: false);

        TolerancePresets.Prefer("second");

        TolerancePresets.Load().Where(p => p.Preferred).Should().ContainSingle(p => p.Name == "second");
    }

    [Fact]
    public void Forgetting_one_leaves_the_rest()
    {
        TolerancePresets.Remember("first", 0.10, preferred: false);
        TolerancePresets.Remember("second", 0.20, preferred: false);

        TolerancePresets.Forget("first");

        TolerancePresets.Load().Should().ContainSingle(p => p.Name == "second");
    }

    /// <summary>A name is matched the way a person would match it, ignoring case and edges.</summary>
    [Fact]
    public void A_name_is_found_however_it_is_typed()
    {
        TolerancePresets.Remember("eSUN PLA+ black - A1", 0.15, preferred: false);

        TolerancePresets.Find(TolerancePresets.Load(), "  esun pla+ BLACK - a1 ")
            .Should().NotBeNull();
    }
}
```

`AppDataFolder` already exists at `tests/Lego2STL.Tests/Run/AppDataFolder.cs`. It points
`LEGO2STL_SETTINGS_DIR` at a temporary directory for the life of the test and puts the old value
back. The collection attribute is required, not optional: the folder is chosen by one process-wide
environment variable, so every class that moves it takes turns. `AppDataDirectoryTests` and
`RunIndexTests` carry the same attribute.

- [ ] **Step 2: Run them and watch them fail**

```
dotnet test tests/Lego2STL.Tests/Lego2STL.Tests.csproj -f net10.0-windows10.0.19041.0 --filter "FullyQualifiedName~TolerancePresets"
```

Expected: FAIL to compile — `TolerancePresets` does not exist.

- [ ] **Step 3: Write the store**

Create `src/Lego2STL.Core/Run/TolerancePresets.cs`:

```csharp
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Lego2STL.Core.Run;

/// <summary>
/// A clearance that was measured once, under the name whoever measured it chose.
/// </summary>
/// <param name="Name">
/// Chosen, not composed. A key of printer and nozzle and material cannot express two spools of
/// the same material that behave differently, or a machine that has drifted since January; a
/// name someone wrote can.
/// </param>
/// <param name="Millimetres">The clearance itself.</param>
/// <param name="Preferred">Whether a build with nothing else to go on should use this one.</param>
public sealed record TolerancePreset(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("millimetres")] double Millimetres,
    [property: JsonPropertyName("preferred")] bool Preferred,
    [property: JsonPropertyName("savedAt")] DateTimeOffset SavedAt);

/// <summary>
/// Where a measured clearance is kept, for both the command line and the window.
/// </summary>
/// <remarks>
/// <para>
/// In Core and not beside the window's other preferences, because the command line does not
/// reference the window's assembly and so cannot read its file. This is the one thing about the
/// design that was forced rather than chosen.
/// </para>
/// <para>
/// A file that cannot be read is treated as no presets. Losing a preference is a far smaller
/// problem than refusing to run, and it is how the window's own preferences already behave.
/// </para>
/// </remarks>
public static class TolerancePresets
{
    public static string FilePath => AppDataDirectory.File("tolerances.json");

    private static readonly JsonSerializerOptions Format = new() { WriteIndented = true };

    public static IReadOnlyList<TolerancePreset> Load()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                return [];
            }

            return JsonSerializer.Deserialize<List<TolerancePreset>>(File.ReadAllText(FilePath)) ?? [];
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    public static void Save(IReadOnlyList<TolerancePreset> presets)
    {
        ArgumentNullException.ThrowIfNull(presets);

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(OnlyOnePreferred(presets), Format));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Not being able to remember a measurement is not worth interrupting anyone over.
        }
    }

    /// <summary>Records a figure under a name, replacing any preset already using that name.</summary>
    public static IReadOnlyList<TolerancePreset> Remember(string name, double millimetres, bool preferred)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var kept = Load().Where(p => !Matches(p, name)).ToList();

        if (preferred)
        {
            kept = [.. kept.Select(p => p with { Preferred = false })];
        }

        kept.Add(new TolerancePreset(name.Trim(), millimetres, preferred, DateTimeOffset.UtcNow));

        var ordered = Ordered(kept);
        Save(ordered);
        return ordered;
    }

    public static IReadOnlyList<TolerancePreset> Prefer(string name)
    {
        var updated = Ordered([.. Load().Select(p => p with { Preferred = Matches(p, name) })]);
        Save(updated);
        return updated;
    }

    public static IReadOnlyList<TolerancePreset> Forget(string name)
    {
        var updated = Ordered([.. Load().Where(p => !Matches(p, name))]);
        Save(updated);
        return updated;
    }

    /// <summary>The preset going by this name, matched the way a person would match it.</summary>
    public static TolerancePreset? Find(IReadOnlyList<TolerancePreset> presets, string? name)
    {
        ArgumentNullException.ThrowIfNull(presets);

        return string.IsNullOrWhiteSpace(name) ? null : presets.FirstOrDefault(p => Matches(p, name));
    }

    private static bool Matches(TolerancePreset preset, string name) =>
        string.Equals(preset.Name.Trim(), name.Trim(), StringComparison.OrdinalIgnoreCase);

    /// <summary>By name, so the list reads the same however it was built up.</summary>
    private static IReadOnlyList<TolerancePreset> Ordered(IEnumerable<TolerancePreset> presets) =>
        [.. presets.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)];

    /// <summary>
    /// The store's own guarantee, applied on the way out.
    /// </summary>
    /// <remarks>
    /// A build silently picking one of two preferred presets would apply a number nobody chose,
    /// so the last one wins here rather than the question being left open.
    /// </remarks>
    private static IReadOnlyList<TolerancePreset> OnlyOnePreferred(IReadOnlyList<TolerancePreset> presets)
    {
        var winner = presets.LastOrDefault(p => p.Preferred);

        return winner is null
            ? presets
            : [.. presets.Select(p => p with { Preferred = ReferenceEquals(p, winner) })];
    }
}
```

- [ ] **Step 4: Run the tests**

```
dotnet test tests/Lego2STL.Tests/Lego2STL.Tests.csproj -f net10.0-windows10.0.19041.0 --filter "FullyQualifiedName~TolerancePresets"
```

Expected: PASS. If `Only_one_preset_is_ever_preferred` fails naming "first", `Remember` is not
clearing the others before adding — the `preferred` branch must run before the new preset is added.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat: a measured clearance can be kept under a name"
```

---

### Task 4: Which clearance a build uses, decided in one place

**Files:**
- Create: `src/Lego2STL.Core/Run/ClearanceChoice.cs`
- Test: `tests/Lego2STL.Tests/Run/ClearanceChoiceTests.cs` (create)

**Interfaces:**
- Consumes: `TolerancePreset`, `TolerancePresets.Find` (Task 3).
- Produces:
  - `sealed record ResolvedClearance(double Millimetres, string? FromPreset)`
  - `sealed class UnknownTolerancePresetException : Exception` with `Name` and `Available`
  - `static ResolvedClearance ClearanceChoice.Resolve(double? asked, string? presetName, IReadOnlyList<TolerancePreset> presets)`

One function, called by the command line and by the window, so the two cannot disagree about what
a preferred preset means.

- [ ] **Step 1: Write the failing tests**

Create `tests/Lego2STL.Tests/Run/ClearanceChoiceTests.cs`:

```csharp
using FluentAssertions;
using Lego2STL.Core.Run;

namespace Lego2STL.Tests.Run;

/// <summary>
/// Which clearance a build uses, and where it came from.
/// </summary>
/// <remarks>
/// One function for both front ends. Resolving this twice - once for the command line, once for
/// the window - is how the two would come to disagree about whether a preferred preset applies,
/// and the disagreement would show up as a plate that printed differently from the same settings.
/// </remarks>
public sealed class ClearanceChoiceTests
{
    private static readonly TolerancePreset Preferred =
        new("black", 0.15, Preferred: true, DateTimeOffset.UnixEpoch);

    private static readonly TolerancePreset Other =
        new("white", 0.20, Preferred: false, DateTimeOffset.UnixEpoch);

    private static readonly IReadOnlyList<TolerancePreset> Both = [Preferred, Other];

    /// <summary>Explicit always beats remembered. That is the whole precedence rule.</summary>
    [Fact]
    public void An_explicit_clearance_beats_every_preset() =>
        ClearanceChoice.Resolve(0.05, "white", Both)
            .Should().Be(new ResolvedClearance(0.05, null));

    /// <summary>
    /// An explicit zero is a real zero, not an unanswered question.
    /// </summary>
    /// <remarks>
    /// The reason the option had to become nullable. While it defaulted to zero there was no way
    /// to say "no clearance, thanks" to a machine that has a preferred preset saved.
    /// </remarks>
    [Fact]
    public void An_explicit_zero_means_no_clearance() =>
        ClearanceChoice.Resolve(0.0, null, Both)
            .Should().Be(new ResolvedClearance(0.0, null));

    [Fact]
    public void A_named_preset_beats_the_preferred_one() =>
        ClearanceChoice.Resolve(null, "white", Both)
            .Should().Be(new ResolvedClearance(0.20, "white"));

    [Fact]
    public void The_preferred_preset_applies_when_nothing_was_asked_for() =>
        ClearanceChoice.Resolve(null, null, Both)
            .Should().Be(new ResolvedClearance(0.15, "black"));

    /// <summary>The refusal to guess survives: no presets, nothing asked, true size.</summary>
    [Fact]
    public void Nothing_asked_and_nothing_preferred_is_true_size() =>
        ClearanceChoice.Resolve(null, null, [Other])
            .Should().Be(new ResolvedClearance(0.0, null));

    [Fact]
    public void Nothing_saved_at_all_is_true_size() =>
        ClearanceChoice.Resolve(null, null, [])
            .Should().Be(new ResolvedClearance(0.0, null));

    /// <summary>
    /// A name nobody saved stops the run rather than quietly meaning zero.
    /// </summary>
    /// <remarks>
    /// A typo that fell back to true size would be discovered after printing, which is the exact
    /// failure this whole feature exists to end.
    /// </remarks>
    [Fact]
    public void A_name_nobody_saved_refuses()
    {
        var act = () => ClearanceChoice.Resolve(null, "blck", Both);

        act.Should().Throw<UnknownTolerancePresetException>()
            .Which.Available.Should().BeEquivalentTo("black", "white");
    }
}
```

- [ ] **Step 2: Run them and watch them fail**

```
dotnet test tests/Lego2STL.Tests/Lego2STL.Tests.csproj -f net10.0-windows10.0.19041.0 --filter "FullyQualifiedName~ClearanceChoice"
```

Expected: FAIL to compile — `ClearanceChoice` does not exist.

- [ ] **Step 3: Write it**

Create `src/Lego2STL.Core/Run/ClearanceChoice.cs`:

```csharp
namespace Lego2STL.Core.Run;

/// <summary>The clearance a build will use, and the preset it came from if it came from one.</summary>
public sealed record ResolvedClearance(double Millimetres, string? FromPreset);

/// <summary>A build asked for a tolerance preset that has not been saved.</summary>
public sealed class UnknownTolerancePresetException : Exception
{
    public UnknownTolerancePresetException(string name, IReadOnlyList<string> available)
        : base($"No tolerance preset is called '{name}'.")
    {
        Name = name;
        Available = available;
    }

    public string Name { get; }

    /// <summary>The names that do exist, so the message can offer them.</summary>
    public IReadOnlyList<string> Available { get; }
}

/// <summary>
/// Decides which clearance a build uses, for both the command line and the window.
/// </summary>
/// <remarks>
/// <para>
/// Most specific first: an explicit figure, then a named preset, then the preferred preset, then
/// nothing at all. Explicit always beats remembered.
/// </para>
/// <para>
/// One function rather than one per front end. Resolving it twice is how the window and the
/// command line would come to disagree about whether a preferred preset applies, and that
/// disagreement would appear as a plate printing differently from the settings that made it.
/// </para>
/// </remarks>
public static class ClearanceChoice
{
    /// <param name="asked">The figure given explicitly, or null when none was. Zero is a figure.</param>
    public static ResolvedClearance Resolve(
        double? asked,
        string? presetName,
        IReadOnlyList<TolerancePreset> presets)
    {
        ArgumentNullException.ThrowIfNull(presets);

        if (asked is { } explicitly)
        {
            return new ResolvedClearance(explicitly, null);
        }

        if (!string.IsNullOrWhiteSpace(presetName))
        {
            return TolerancePresets.Find(presets, presetName) is { } named
                ? new ResolvedClearance(named.Millimetres, named.Name)
                : throw new UnknownTolerancePresetException(
                    presetName.Trim(),
                    [.. presets.Select(p => p.Name)]);
        }

        return presets.FirstOrDefault(p => p.Preferred) is { } preferred
            ? new ResolvedClearance(preferred.Millimetres, preferred.Name)
            : new ResolvedClearance(0.0, null);
    }
}
```

- [ ] **Step 4: Run the tests**

```
dotnet test tests/Lego2STL.Tests/Lego2STL.Tests.csproj -f net10.0-windows10.0.19041.0 --filter "FullyQualifiedName~ClearanceChoice"
```

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat: an explicit clearance beats a named one, which beats a remembered one"
```

---

### Task 5: A run records where its clearance came from

**Files:**
- Modify: `src/Lego2STL.Core/Pipeline/RunSettings.cs` (add `ClearanceFrom`; the command-line
  reconstruction near `if (Clearance > 0)`)
- Modify: `src/Lego2STL.Core/Run/ManifestSettings.cs` (add `ClearanceFrom`, and wherever
  `ManifestSettings` is built from `RunSettings`)
- Modify: `src/Lego2STL.Core/Text/TextKey.cs`, `Strings.English.cs`, `Strings.Italian.cs`
- Modify: `src/Lego2STL.Core/Pipeline/PipelineRunner.cs` (log it once, beside the other geometry
  messages)
- Test: `tests/Lego2STL.Tests/Run/RunManifestTests.cs` (add to it)

**Interfaces:**
- Consumes: `ResolvedClearance` (Task 4).
- Produces: `RunSettings.ClearanceFrom` (`string?`, `init`), `ManifestSettings.ClearanceFrom`
  (`string?`, `init`), `TextKey.MsgClearanceFromPreset`.

`RunSettings.Clearance` stays a plain `double` holding the **resolved** figure. The nullability
lives in the command-line option and in `ClearanceChoice.Resolve`; keeping it out of `RunSettings`
avoids rippling through `MeshOptions`, `Validate` and the command-line reconstruction for no gain.

- [ ] **Step 1: Write the failing test**

Add to `tests/Lego2STL.Tests/Run/RunManifestTests.cs`:

```csharp
    /// <summary>
    /// A clearance that came from a preset says so, because nothing on the command line will.
    /// </summary>
    /// <remarks>
    /// This is the price of a preferred preset applying without being asked for. Every other
    /// decision the tool makes is already recorded, including how each part was laid on the bed,
    /// and a number that changes every dimension of every shape is not the one to leave out.
    /// </remarks>
    [Fact]
    public async Task Where_the_clearance_came_from_is_recorded()
    {
        var layout = ARunFolder();
        var outcome = APretendRun.Complete(layout) with
        {
            Settings = APretendRun.ASetting() with
            {
                Clearance = 0.15,
                ClearanceFrom = "eSUN PLA+ black - A1",
            },
        };

        var manifest = RunManifest.From(outcome, APretendRun.Started, APretendRun.Finished, null);

        manifest.Settings.ClearanceFrom.Should().Be("eSUN PLA+ black - A1");

        await RunManifest.WriteAsync(layout, manifest);
        var (read, state) = RunManifest.Read(layout.ManifestPath);

        state.Should().Be(ManifestState.Present);
        read!.Settings.ClearanceFrom.Should().Be("eSUN PLA+ black - A1");
    }
```

`RunOutcome.Settings` is a `required RunSettings` (`RunOutcome.cs:38`) and `APretendRun.ASetting()`
returns one, so the `with` above compiles as written.

- [ ] **Step 2: Run it and watch it fail**

```
dotnet test tests/Lego2STL.Tests/Lego2STL.Tests.csproj -f net10.0-windows10.0.19041.0 --filter "FullyQualifiedName~Where_the_clearance_came_from"
```

Expected: FAIL to compile — `ClearanceFrom` is on neither record.

- [ ] **Step 3: Carry it through both records**

In `RunSettings.cs`, immediately after `public double Clearance { get; init; }`:

```csharp
    /// <summary>The tolerance preset the clearance came from, when it came from one.</summary>
    public string? ClearanceFrom { get; init; }
```

In `ManifestSettings.cs`, immediately after `public double Clearance { get; init; }`:

```csharp
    public string? ClearanceFrom { get; init; }
```

Then find where `ManifestSettings` is built from `RunSettings` — search for `Clearance = ` in
`src/Lego2STL.Core/Run/` — and pass `ClearanceFrom = settings.ClearanceFrom` beside it.

- [ ] **Step 4: Say it once, in both languages**

In `TextKey.cs`, beside `MsgClearanceApplied`:

```csharp
    MsgClearanceFromPreset,
```

In `Strings.English.cs`, beside `[TextKey.MsgClearanceApplied]`:

```csharp
            [TextKey.MsgClearanceFromPreset] =
                "Clearance {0} mm, from the saved tolerance '{1}'.",
```

In `Strings.Italian.cs`, in the same place:

```csharp
            [TextKey.MsgClearanceFromPreset] =
                "Tolleranza {0} mm, dal preset salvato '{1}'.",
```

- [ ] **Step 5: Say it in the two places the clearance is already reported**

There are exactly two, and neither is in `PipelineRunner`: `ConsoleRun.ReportClearance` writes the
line a person sees, and `RunReport` writes the one in `report.txt`.

In `src/Lego2STL.Cli/ConsoleRun.cs`, inside `ReportClearance`, after the early return and before
the existing `Console.WriteLine`:

```csharp
        // Said out loud because a preferred tolerance applies without appearing on the command
        // line, and a number that changes every dimension of every shape should not arrive silently.
        if (outcome.Settings.ClearanceFrom is { Length: > 0 } preset)
        {
            Console.WriteLine("  " + words.Format(
                TextKey.MsgClearanceFromPreset, outcome.Settings.Clearance, preset));
        }
```

In `src/Lego2STL.Core/Pipeline/RunReport.cs`, inside the `if (outcome.Settings.Clearance > 0)` block
at `:187`, after the existing `sb.AppendLine`:

```csharp
            if (outcome.Settings.ClearanceFrom is { Length: > 0 } preset)
            {
                sb.AppendLine(words.Format(
                    TextKey.MsgClearanceFromPreset, outcome.Settings.Clearance, preset));
            }
```

`ReportClearance` returns early when the clearance is zero, so a preset that saved a zero says
nothing — which is right: nothing happened to the geometry.

- [ ] **Step 6: Run the whole suite**

```
dotnet test Lego2STL.slnx
```

Expected: PASS, including the completeness check over every `TextKey` in both languages.

- [ ] **Step 7: Commit**

```bash
git add -A
git commit -m "feat: a run says which saved tolerance its clearance came from"
```

---

### Task 6: `build --tolerances`, and an explicit zero that means zero

**Files:**
- Modify: `src/Lego2STL.Cli/Commands/PipelineOptions.cs:77-79` (the `--clearance` option), `:168`,
  `:212`, `:253`
- Modify: `src/Lego2STL.Core/Text/TextKey.cs`, `Strings.English.cs`, `Strings.Italian.cs`
- Modify: `tests/Lego2STL.UiTests/OptionParityTests.cs:59` (the pinned option count)
- Test: `tests/Lego2STL.UiTests/TolerancesReachABuildTests.cs` (create)

**Interfaces:**
- Consumes: `ClearanceChoice.Resolve`, `UnknownTolerancePresetException` (Task 4),
  `TolerancePresets.Load` (Task 3), `RunSettings.ClearanceFrom` (Task 5).
- Produces: `PipelineOptions.Tolerances` (`Option<string?>`, flag `--tolerances`);
  `PipelineOptions.Clearance` becomes `Option<double?>`; `TextKey.HelpOptTolerances`,
  `TextKey.ErrUnknownTolerancePreset`.

**`OptionParityTests` walks the real command-line declaration**, so adding `--tolerances` here will
fail the window's parity test until Task 10 adds the matching control. That failure is expected and
is the test doing its job; Task 6 leaves the suite red on that one test and Task 10 turns it green.
Do not weaken the parity test to get past it.

- [ ] **Step 1: Write the failing tests**

Create `tests/Lego2STL.UiTests/TolerancesReachABuildTests.cs`:

```csharp
using System;
using System.CommandLine;
using System.IO;
using FluentAssertions;
using Lego2STL.Cli.Commands;
using Lego2STL.Core.Catalogue;
using Lego2STL.Core.Pipeline;
using Lego2STL.Core.Run;
using Lego2STL.Core.Text;

namespace Lego2STL.UiTests;

/// <summary>
/// A saved tolerance reaching a build, through the real option declaration.
/// </summary>
/// <remarks>
/// Driven through PipelineOptions rather than through the resolver alone, because the wiring
/// between the two is the part that can be wrong without any test noticing: the resolver can be
/// perfect and simply never be called. In this project rather than beside the other pipeline
/// tests because this one is the only assembly the command line shows its internals to.
/// </remarks>
public sealed class TolerancesReachABuildTests : IDisposable
{
    /// <summary>
    /// This assembly shares one settings folder, so the store is emptied rather than moved.
    /// </summary>
    public TolerancesReachABuildTests() => Clear();

    public void Dispose() => Clear();

    private static void Clear()
    {
        if (File.Exists(TolerancePresets.FilePath))
        {
            File.Delete(TolerancePresets.FilePath);
        }
    }

    /// <summary>The real declaration, parsed and read exactly as the build command reads it.</summary>
    private static RunSettings ParseBuild(params string[] args)
    {
        var options = new PipelineOptions(Strings.English);
        var command = new Command("build");
        options.AddTo(command, includeDocumentOptions: false);
        command.Options.Add(CommonOptions.Language);

        return options.Read(command.Parse(args), InputKind.PartsList, "parts.csv", null);
    }

    [Fact]
    public void A_named_tolerance_supplies_the_clearance()
    {
        TolerancePresets.Remember("black", 0.15, preferred: false);

        var settings = ParseBuild("--tolerances", "black");

        settings.Clearance.Should().Be(0.15);
        settings.ClearanceFrom.Should().Be("black");
    }

    [Fact]
    public void A_preferred_tolerance_supplies_it_with_nothing_asked_for()
    {
        TolerancePresets.Remember("black", 0.15, preferred: true);

        var settings = ParseBuild();

        settings.Clearance.Should().Be(0.15);
        settings.ClearanceFrom.Should().Be("black");
    }

    /// <summary>The reason the option had to become nullable.</summary>
    [Fact]
    public void An_explicit_zero_turns_a_preferred_tolerance_off()
    {
        TolerancePresets.Remember("black", 0.15, preferred: true);

        var settings = ParseBuild("--clearance", "0");

        settings.Clearance.Should().Be(0);
        settings.ClearanceFrom.Should().BeNull();
    }

    /// <summary>The refusal to guess, still standing.</summary>
    [Fact]
    public void Nothing_saved_leaves_a_build_at_true_size()
    {
        var settings = ParseBuild();

        settings.Clearance.Should().Be(0);
        settings.ClearanceFrom.Should().BeNull();
    }

    /// <summary>An explicit figure still beats a saved one, which is the whole precedence rule.</summary>
    [Fact]
    public void An_explicit_clearance_still_wins()
    {
        TolerancePresets.Remember("black", 0.15, preferred: true);

        ParseBuild("--clearance", "0.05").Clearance.Should().Be(0.05);
    }
}
```

`InputKind` comes from wherever `BuildCommand` gets it — check its `using` block and copy it, since
the enum's namespace is the one thing above not read off the file.

- [ ] **Step 2: Run them and watch them fail**

```
dotnet test tests/Lego2STL.UiTests/Lego2STL.UiTests.csproj -f net10.0-windows10.0.19041.0 --filter "FullyQualifiedName~TolerancesReachABuild"
```

Expected: FAIL to compile — `PipelineOptions` has no `Tolerances`, and `RunSettings` no
`ClearanceFrom` until Task 5 is in.

- [ ] **Step 3: Add the option and make `--clearance` nullable**

In `PipelineOptions.cs`, change the `--clearance` declaration to:

```csharp
        Clearance = new Option<double?>("--clearance")
        {
            Description = words[TextKey.HelpOptClearance],
        };
```

and its property to `public Option<double?> Clearance { get; }`. Add beside it:

```csharp
        Tolerances = new Option<string?>("--tolerances")
        {
            Description = words[TextKey.HelpOptTolerances],
        };
```

with `public Option<string?> Tolerances { get; }`, and add `Tolerances` to the list of options the
command registers at `:212`.

- [ ] **Step 4: Resolve at the point the settings are built**

At `:253`, where `Clearance = parseResult.GetValue(Clearance),` is today, remove that line and
instead resolve after the object is constructed. The exact shape depends on how the method builds
its `RunSettings`; the resolution itself is:

```csharp
        // One resolver for both front ends, so the window and the command line cannot come to
        // disagree about whether a preferred tolerance applies.
        var clearance = ClearanceChoice.Resolve(
            parseResult.GetValue(Clearance),
            parseResult.GetValue(Tolerances),
            TolerancePresets.Load());
```

and then `Clearance = clearance.Millimetres, ClearanceFrom = clearance.FromPreset,` in the
initialiser.

- [ ] **Step 5: Turn an unknown name into a refusal the user can act on**

`Program.Main` already wraps `root.Parse(args).InvokeAsync()` in a try, catching
`OperationCanceledException` and then everything else — printing `ex.Message` on the argument that
*"everything the tool throws deliberately carries a message meant for the user"*. That generic
catch would already handle this one, but its message cannot list the saved names in the run's
language, which is the half that makes it useful. So add a specific catch **before** the generic
one:

```csharp
        catch (UnknownTolerancePresetException ex)
        {
            // Named separately from the catch below so the message can offer the names that do
            // exist, which is what turns a refusal into something the reader can act on.
            Console.Error.WriteLine($"{words[TextKey.MsgError]}: " + words.Format(
                TextKey.ErrUnknownTolerancePreset, ex.Name, string.Join(", ", ex.Available)));

            return ExitFailure;
        }
```

Add the wording. In `TextKey.cs`:

```csharp
    HelpOptTolerances,
    ErrUnknownTolerancePreset,
```

In `Strings.English.cs`:

```csharp
            [TextKey.HelpOptTolerances] =
                "Take the clearance from a saved tolerance, by name. An explicit --clearance wins.",
            [TextKey.ErrUnknownTolerancePreset] =
                "No saved tolerance is called '{0}'. The ones that are: {1}.",
```

In `Strings.Italian.cs`:

```csharp
            [TextKey.HelpOptTolerances] =
                "Prende la tolleranza da un preset salvato, per nome. Un --clearance esplicito vince.",
            [TextKey.ErrUnknownTolerancePreset] =
                "Nessuna tolleranza salvata si chiama '{0}'. Quelle che ci sono: {1}.",
```

- [ ] **Step 6: Move the pinned option count**

`OptionParityTests.The_command_line_registers_the_options_this_test_expects_to_find` pins the count
at 26 so that a shrinking option set cannot make the parity test quietly check less. Adding
`--tolerances` makes it 27:

```csharp
        EveryFlag().Should().HaveCount(27)
            .And.Contain("--quiet")
            .And.Contain("--tolerances")
```

Leave the other `.And.Contain` lines as they are.

- [ ] **Step 7: Run the whole suite**

```
dotnet test Lego2STL.slnx
```

Expected: PASS **except** `OptionParityTests.Every_option_the_command_line_takes_is_named_in_the_window`,
which now reports `--tolerances` as missing from the window. That is correct and Task 12 fixes it.
Every other test must pass, the pinned-count test included; in particular the existing coverage of
`--clearance` must still pass, since `--clearance 0.15` resolves to 0.15 exactly as before.

- [ ] **Step 8: Commit**

```bash
git add -A
git commit -m "feat: a build can take its clearance from a saved tolerance"
```

---

### Task 7: `calibration --save`, and the other three

**Files:**
- Modify: `src/Lego2STL.Cli/Commands/CalibrationCommand.cs`
- Modify: `src/Lego2STL.Core/Text/TextKey.cs`, `Strings.English.cs`, `Strings.Italian.cs`
- Test: `tests/Lego2STL.Tests/Run/TolerancePresetsTests.cs` already covers the store; add
  `tests/Lego2STL.UiTests/CalibrationManagementTests.cs` (create) for the command

**Interfaces:**
- Consumes: `TolerancePresets.Remember`, `.Prefer`, `.Forget`, `.Load` (Task 3).
- Produces: on `calibration`, the flags `--save <mm>`, `--name <text>`, `--preferred`, `--list`,
  `--prefer <name>`, `--forget <name>`. Each of these records or reports and returns without
  building anything.

The roadmap sketched step 3 as `calibration --save`, and the spec adopts it. The cost is that
`calibration` now has two modes. **If a fifth management flag is ever wanted, split them into a
command of their own instead of adding it.**

- [ ] **Step 1: Write the failing tests**

Create `tests/Lego2STL.UiTests/CalibrationManagementTests.cs`:

```csharp
using System;
using System.CommandLine;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Lego2STL.Cli.Commands;
using Lego2STL.Core.Run;
using Lego2STL.Core.Text;

namespace Lego2STL.UiTests;

/// <summary>
/// The half of the calibration command that records a measurement instead of building one.
/// </summary>
/// <remarks>
/// Two modes in one command is a real cost, taken deliberately: the roadmap asked for
/// calibration --save and the alternative was a second command for four flags. The tests here
/// exist mostly to hold the line that the management flags never build a plate, which is the way
/// two modes in one command goes wrong.
/// </remarks>
public sealed class CalibrationManagementTests : IDisposable
{
    private readonly string _output = Path.Combine(
        Path.GetTempPath(), "lego2stl-calmgmt-" + Guid.NewGuid().ToString("N"));

    /// <summary>This assembly shares one settings folder, so the store is emptied, not moved.</summary>
    public CalibrationManagementTests() => Clear();

    public void Dispose()
    {
        Clear();

        if (Directory.Exists(_output))
        {
            Directory.Delete(_output, recursive: true);
        }
    }

    private static void Clear()
    {
        if (File.Exists(TolerancePresets.FilePath))
        {
            File.Delete(TolerancePresets.FilePath);
        }
    }

    /// <summary>
    /// The command as the parser really assembles it.
    /// </summary>
    /// <remarks>
    /// Wrapped in a root command so the arguments read the way a person types them, and so the
    /// test drives the declaration the user drives rather than a copy of it.
    /// </remarks>
    internal static async Task<int> RunAsync(params string[] args)
    {
        var words = Strings.English;
        var root = new RootCommand("test") { CalibrationCommand.Create(words) };

        return await root.Parse(args).InvokeAsync().ConfigureAwait(false);
    }

    [Fact]
    public async Task Saving_a_measurement_records_it_and_builds_nothing()
    {
        var exit = await RunAsync("calibration", "--save", "0.15", "--name", "black",
            "--output-dir", _output);

        exit.Should().Be(0);
        TolerancePresets.Load().Should().ContainSingle(p => p.Name == "black" && p.Millimetres == 0.15);
        Directory.Exists(_output).Should().BeFalse("nothing was built, so nothing was written");
    }

    [Fact]
    public async Task Saving_can_mark_it_preferred_at_the_same_time()
    {
        await RunAsync("calibration", "--save", "0.15", "--name", "black", "--preferred");

        TolerancePresets.Load().Should().ContainSingle(p => p.Preferred);
    }

    /// <summary>A figure with no name is refused: an unnamed preset cannot be asked for again.</summary>
    [Fact]
    public async Task Saving_without_a_name_is_refused()
    {
        var exit = await RunAsync("calibration", "--save", "0.15");

        exit.Should().NotBe(0);
        TolerancePresets.Load().Should().BeEmpty();
    }

    [Fact]
    public async Task Preferring_and_forgetting_do_what_they_say()
    {
        await RunAsync("calibration", "--save", "0.10", "--name", "first");
        await RunAsync("calibration", "--save", "0.20", "--name", "second");

        await RunAsync("calibration", "--prefer", "second");
        TolerancePresets.Load().Should().ContainSingle(p => p.Preferred && p.Name == "second");

        await RunAsync("calibration", "--forget", "first");
        TolerancePresets.Load().Should().ContainSingle(p => p.Name == "second");
    }

    [Fact]
    public async Task Listing_builds_nothing()
    {
        await RunAsync("calibration", "--save", "0.15", "--name", "black");

        var exit = await RunAsync("calibration", "--list", "--output-dir", _output);

        exit.Should().Be(0);
        Directory.Exists(_output).Should().BeFalse();
    }
}
```

- [ ] **Step 2: Run them and watch them fail**

```
dotnet test tests/Lego2STL.UiTests/Lego2STL.UiTests.csproj -f net10.0-windows10.0.19041.0 --filter "FullyQualifiedName~CalibrationManagement"
```

Expected: FAIL — `--save` is not a known option.

- [ ] **Step 3: Add the six flags**

In `CalibrationCommand.Create`, beside the existing options:

```csharp
        var save = new Option<double?>("--save")
        {
            Description = words[TextKey.HelpOptSave],
        };

        var name = new Option<string?>("--name")
        {
            Description = words[TextKey.HelpOptToleranceName],
        };

        var preferred = new Option<bool>("--preferred")
        {
            Description = words[TextKey.HelpOptPreferred],
        };

        var list = new Option<bool>("--list")
        {
            Description = words[TextKey.HelpOptListTolerances],
        };

        var prefer = new Option<string?>("--prefer")
        {
            Description = words[TextKey.HelpOptPrefer],
        };

        var forget = new Option<string?>("--forget")
        {
            Description = words[TextKey.HelpOptForget],
        };
```

Register them on the command beside the others, and in `SetAction` pass their values to `RunAsync`.

- [ ] **Step 4: Branch to the management mode before anything is built**

At the very top of `CalibrationCommand.RunAsync`, before the output directory is created:

```csharp
        // The management flags record or report and stop. Placed before anything is created so
        // that asking what is saved never leaves a folder behind.
        if (list)
        {
            foreach (var preset in TolerancePresets.Load())
            {
                Console.WriteLine(string.Create(
                    CultureInfo.InvariantCulture,
                    $"  {(preset.Preferred ? "*" : " ")} {preset.Name,-40}{preset.Millimetres,8:0.00} mm"));
            }

            return Program.ExitOk;
        }

        if (prefer is { Length: > 0 })
        {
            TolerancePresets.Prefer(prefer);
            return Program.ExitOk;
        }

        if (forget is { Length: > 0 })
        {
            TolerancePresets.Forget(forget);
            return Program.ExitOk;
        }

        if (save is { } measured)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                Console.Error.WriteLine($"{words[TextKey.MsgError]}: {words[TextKey.ErrToleranceNeedsAName]}");
                return Program.ExitFailure;
            }

            TolerancePresets.Remember(name, measured, preferred);
            Console.WriteLine(words.Format(TextKey.MsgToleranceSaved, name, measured));
            return Program.ExitOk;
        }
```

- [ ] **Step 5: Add the wording**

In `TextKey.cs`:

```csharp
    HelpOptSave,
    HelpOptToleranceName,
    HelpOptPreferred,
    HelpOptListTolerances,
    HelpOptPrefer,
    HelpOptForget,
    ErrToleranceNeedsAName,
    MsgToleranceSaved,
```

In `Strings.English.cs`:

```csharp
            [TextKey.HelpOptSave] =
                "Record a clearance you measured, in millimetres, and build nothing.",
            [TextKey.HelpOptToleranceName] =
                "What to call the clearance being saved. Something you will recognise: the spool "
                + "and the machine, not a code.",
            [TextKey.HelpOptPreferred] =
                "Mark the clearance being saved as the one a build uses when none is asked for.",
            [TextKey.HelpOptListTolerances] = "List the clearances saved so far, and build nothing.",
            [TextKey.HelpOptPrefer] =
                "Make this saved clearance the one a build uses when none is asked for.",
            [TextKey.HelpOptForget] = "Remove a saved clearance.",
            [TextKey.ErrToleranceNeedsAName] =
                "A clearance needs a name to be saved under, or nothing can ask for it again. "
                + "Add --name.",
            [TextKey.MsgToleranceSaved] = "Saved '{0}' as {1} mm.",
```

In `Strings.Italian.cs`:

```csharp
            [TextKey.HelpOptSave] =
                "Registra una tolleranza misurata, in millimetri, e non costruisce nulla.",
            [TextKey.HelpOptToleranceName] =
                "Come chiamare la tolleranza da salvare. Qualcosa di riconoscibile: la bobina e la "
                + "macchina, non un codice.",
            [TextKey.HelpOptPreferred] =
                "Segna la tolleranza da salvare come quella che una build usa quando non se ne chiede una.",
            [TextKey.HelpOptListTolerances] =
                "Elenca le tolleranze salvate finora, e non costruisce nulla.",
            [TextKey.HelpOptPrefer] =
                "Rende questa tolleranza salvata quella che una build usa quando non se ne chiede una.",
            [TextKey.HelpOptForget] = "Rimuove una tolleranza salvata.",
            [TextKey.ErrToleranceNeedsAName] =
                "Una tolleranza ha bisogno di un nome per essere salvata, altrimenti niente potrà "
                + "richiederla. Aggiungi --name.",
            [TextKey.MsgToleranceSaved] = "Salvata '{0}' come {1} mm.",
```

- [ ] **Step 6: Run the whole suite**

```
dotnet test Lego2STL.slnx
```

Expected: PASS except `OptionParityTests`, still red from Task 6. Note that `OptionParityTests`
covers the **pipeline** commands, not `calibration`, so these six flags do not need a window
control.

- [ ] **Step 7: Commit**

```bash
git add -A
git commit -m "feat: the clearance you measured can be saved, listed, preferred and forgotten"
```

---

### Task 8: The calibration set, as one plate

**Files:**
- Create: `src/Lego2STL.Core/Plates/CalibrationSet.cs`
- Test: `tests/Lego2STL.Tests/Plates/CalibrationSetTests.cs` (create)

**Interfaces:**
- Consumes: `PlateItem`, `PlateWriter.WritePlatesAsync` (Task 1), `MeshPipeline.Prepare`,
  `PartMesh`.
- Produces:
  - `sealed record CalibrationPiece(string PartNumber, double Millimetres, string Label)`
  - `static class CalibrationSet` with `PartNumbers → IReadOnlyList<string>`,
    `WitnessLabel → string`, `DefaultSteps → IReadOnlyList<double>`, and
    `Items(IReadOnlyDictionary<string, PartMesh> sources, IReadOnlyList<double> steps, MeshPipelineOptions template) → (IReadOnlyList<PlateItem> Items, IReadOnlyList<string> Missing)`

`DefaultSteps` moves here from `CalibrationCommand`, which keeps the six numbers in one place now
that the window builds the same plate.

Measured from the tool itself on 2026-09-01: `3705` 31.6 × 4.8, `4265c` 7.2 × 4, `3003` 16 × 16,
`3700` 16 × 8, `3673` 16 × 6.4, `3035` 64 × 32 mm. Six pieces at six steps plus the witness is
about 7 600 mm² against an A1's 60 500 mm² of usable bed.

- [ ] **Step 1: Write the failing tests**

Create `tests/Lego2STL.Tests/Plates/CalibrationSetTests.cs`:

```csharp
using System.Numerics;
using FluentAssertions;
using Lego2STL.Core.Geometry;
using Lego2STL.Core.LDraw;
using Lego2STL.Core.Plates;

namespace Lego2STL.Tests.Plates;

/// <summary>
/// What goes on a calibration plate.
/// </summary>
/// <remarks>
/// Three mating pairs at every clearance, because a fit is a property of two parts and not of
/// one, and one wide plate printed once. The wide plate is not part of the matrix: it tests
/// warping, which no clearance value changes, and printing it six times would spend bed and
/// filament varying something along an axis that does not affect it.
/// </remarks>
public sealed class CalibrationSetTests
{
    private static PartMesh ABlockCalled(string number)
    {
        var t = new List<Triangle>
        {
            new(new Vector3(0, 0, 0), new Vector3(10, 0, 0), new Vector3(0, 10, 0)),
            new(new Vector3(0, 0, 0), new Vector3(0, 10, 0), new Vector3(0, 0, 10)),
            new(new Vector3(0, 0, 0), new Vector3(0, 0, 10), new Vector3(10, 0, 0)),
            new(new Vector3(10, 0, 0), new Vector3(0, 0, 10), new Vector3(0, 10, 0)),
        };

        return new PartMesh(number, number, t, null, 1, []);
    }

    private static Dictionary<string, PartMesh> AllOfThem() =>
        CalibrationSet.PartNumbers.ToDictionary(n => n, ABlockCalled, StringComparer.OrdinalIgnoreCase);

    private static readonly double[] SixSteps = [0.00, 0.05, 0.10, 0.15, 0.20, 0.25];

    /// <summary>Six pieces at six clearances, and the witness once.</summary>
    [Fact]
    public void The_matrix_is_every_pair_at_every_step_and_the_witness_once()
    {
        var (items, missing) = CalibrationSet.Items(AllOfThem(), SixSteps, new MeshPipelineOptions());

        missing.Should().BeEmpty();

        // Two bricks 2x2 make the stud pair, so six pieces per step, not five.
        items.Where(i => i.Label != CalibrationSet.WitnessLabel).Should().HaveCount(6 * 6);
        items.Should().ContainSingle(i => i.Label == CalibrationSet.WitnessLabel);
    }

    /// <summary>Every label says its clearance, because that is what the sheet maps.</summary>
    [Fact]
    public void Every_label_carries_the_clearance_it_was_built_at()
    {
        var (items, _) = CalibrationSet.Items(AllOfThem(), SixSteps, new MeshPipelineOptions());

        items.Should().Contain(i => i.Label == "3705-0.15mm");
        items.Should().Contain(i => i.Label == "3673-0.25mm");
    }

    /// <summary>
    /// A part the library has not got is left off and named, rather than stopping the plate.
    /// </summary>
    /// <remarks>
    /// The old behaviour was to abort the whole command, which is right when the output is that
    /// one part and wrong for a plate whose value is mostly still there. Two of three fits are
    /// still worth printing.
    /// </remarks>
    [Fact]
    public void A_part_the_library_has_not_got_is_left_off_and_named()
    {
        var sources = AllOfThem();
        sources.Remove("3673");

        var (items, missing) = CalibrationSet.Items(sources, SixSteps, new MeshPipelineOptions());

        missing.Should().Contain("3673");
        items.Should().NotBeEmpty();
        items.Should().NotContain(i => i.Label.StartsWith("3673", StringComparison.Ordinal));
    }

    /// <summary>The witness is built once, at no clearance, whatever the steps are.</summary>
    [Fact]
    public void The_witness_is_built_at_no_clearance()
    {
        var (items, _) = CalibrationSet.Items(AllOfThem(), SixSteps, new MeshPipelineOptions());

        CalibrationSet.WitnessLabel.Should().EndWith("0.00mm");
        items.Should().ContainSingle(i => i.Label == CalibrationSet.WitnessLabel);
    }
}
```

- [ ] **Step 2: Run them and watch them fail**

```
dotnet test tests/Lego2STL.Tests/Lego2STL.Tests.csproj -f net10.0-windows10.0.19041.0 --filter "FullyQualifiedName~CalibrationSet"
```

Expected: FAIL to compile — `CalibrationSet` does not exist.

- [ ] **Step 3: Write it**

Create `src/Lego2STL.Core/Plates/CalibrationSet.cs`:

```csharp
using System.Globalization;
using Lego2STL.Core.Geometry;
using Lego2STL.Core.LDraw;

namespace Lego2STL.Core.Plates;

/// <summary>
/// What goes on a calibration plate, and at which clearances.
/// </summary>
/// <remarks>
/// <para>
/// Three mating pairs, because a fit is a property of two parts and not of one, and each pair
/// tests a different joint: an axle in a bush, a stud in a tube, a pin in a Technic hole. The
/// clearance applies to both halves, so a pair at 0.15 has 0.30 mm of gap - which is exactly what
/// a real build produces, where both parts come off the same machine at the same setting.
/// </para>
/// <para>
/// One number comes out of all this, not one per pair. The pipeline insets every face of every
/// part by a single figure and has no way to treat a stud differently from an axle, so the extra
/// pairs are here to check one figure against several joints rather than to produce several.
/// </para>
/// <para>
/// The wide plate is not part of the matrix. It tests warping, which no clearance value changes,
/// and printing it at six clearances would spend bed and filament varying something along an axis
/// that does not affect it. It is here once because it is the check that says whether any of the
/// other readings mean anything.
/// </para>
/// </remarks>
public static class CalibrationSet
{
    /// <summary>The mating pairs, and how many of each go on at every clearance.</summary>
    private static readonly (string PartNumber, int Count)[] Matrix =
    [
        ("3705", 1),   // Technic Axle 4
        ("4265c", 1),  // Technic Bush
        ("3003", 2),   // Brick 2 x 2, twice: a stud fit needs something to go into
        ("3700", 1),   // Technic Brick 1 x 2 with hole
        ("3673", 1),   // Technic Pin
    ];

    /// <summary>The wide plate that says whether the bed and the first layer are right at all.</summary>
    private const string Witness = "3035";

    /// <summary>
    /// The clearances tried, in millimetres.
    /// </summary>
    /// <remarks>
    /// Here rather than on the command, because the window builds the same plate and two copies
    /// of six numbers is one copy too many.
    /// </remarks>
    public static IReadOnlyList<double> DefaultSteps { get; } = [0.00, 0.05, 0.10, 0.15, 0.20, 0.25];

    public static string WitnessLabel => LabelFor(Witness, 0.0);

    /// <summary>Everything the plate needs from the library, each named once.</summary>
    public static IReadOnlyList<string> PartNumbers =>
        [.. Matrix.Select(m => m.PartNumber).Append(Witness).Distinct(StringComparer.OrdinalIgnoreCase)];

    /// <param name="sources">What the library gave back, by part number. A gap here is a missing part.</param>
    /// <param name="template">The pipeline options to build each piece with; its clearance is replaced.</param>
    public static (IReadOnlyList<PlateItem> Items, IReadOnlyList<string> Missing) Items(
        IReadOnlyDictionary<string, PartMesh> sources,
        IReadOnlyList<double> steps,
        MeshPipelineOptions template)
    {
        ArgumentNullException.ThrowIfNull(sources);
        ArgumentNullException.ThrowIfNull(steps);
        ArgumentNullException.ThrowIfNull(template);

        var items = new List<PlateItem>();
        var missing = new List<string>();

        foreach (var (partNumber, count) in Matrix)
        {
            if (!sources.TryGetValue(partNumber, out var source))
            {
                missing.Add(partNumber);
                continue;
            }

            foreach (var step in steps)
            {
                items.Add(new PlateItem(LabelFor(partNumber, step), Built(source, template, step), count));
            }
        }

        if (sources.TryGetValue(Witness, out var wide))
        {
            items.Add(new PlateItem(WitnessLabel, Built(wide, template, 0.0), 1));
        }
        else
        {
            missing.Add(Witness);
        }

        return (items, missing);
    }

    private static IndexedMesh Built(PartMesh source, MeshPipelineOptions template, double step) =>
        MeshPipeline.Prepare(source, template with
        {
            // Covered whatever was asked for: a calibration piece that silently came out at true
            // size would send the whole exercise wrong.
            FillGaps = true,
            ClearanceMillimetres = (float)step,
        }).Mesh;

    private static string LabelFor(string partNumber, double step) =>
        string.Create(CultureInfo.InvariantCulture, $"{partNumber}-{step:0.00}mm");
}
```

- [ ] **Step 4: Run the tests**

```
dotnet test tests/Lego2STL.Tests/Lego2STL.Tests.csproj -f net10.0-windows10.0.19041.0 --filter "FullyQualifiedName~CalibrationSet"
```

Expected: PASS. If the count is 30 rather than 36, the `3003` entry lost its count of 2 — a stud
fit needs a brick to press onto another brick.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat: a calibration tests three kinds of fit and whether the bed is flat"
```

---

### Task 9: The calibration sheet, with a map of the plate

**Files:**
- Create: `src/Lego2STL.Core/Plates/CalibrationNotes.cs`
- Modify: `src/Lego2STL.Core/Text/TextKey.cs`, `Strings.English.cs`, `Strings.Italian.cs`
- Test: `tests/Lego2STL.Tests/Plates/CalibrationNotesTests.cs` (create)

**Interfaces:**
- Consumes: `PrintNotes.Settings` (Task 2), `PlacedItem` and `PackedPlate` from `ShelfPacker`,
  `CalibrationSet.WitnessLabel` (Task 8).
- Produces: `static string CalibrationNotes.Write(PackedPlate plate, IReadOnlyList<string> missing, string? printer, Strings words)`

The map is **generated from the placement the packer returned** — row by row, front to back, each
row left to right. `ShelfPacker` sorts by depth, then width, then label, so a map built from the
order the items were handed over would be wrong; and clearance changes footprints, which is the
whole point of the plate.

- [ ] **Step 1: Write the failing tests**

Create `tests/Lego2STL.Tests/Plates/CalibrationNotesTests.cs`:

```csharp
using System.Numerics;
using FluentAssertions;
using Lego2STL.Core.Plates;
using Lego2STL.Core.Text;

namespace Lego2STL.Tests.Plates;

/// <summary>
/// The sheet beside a calibration plate.
/// </summary>
/// <remarks>
/// One sheet, not two. A build plate's folder gets how-to-print.txt; this folder keeps its own
/// single sheet and that sheet carries the print settings too, because two overlapping
/// instruction files in one folder is exactly the confusion the note was written to prevent.
/// </remarks>
public sealed class CalibrationNotesTests
{
    private static PackedPlate APlateWhereTheOrderChanged()
    {
        // Handed over as A, B, C; placed as C, B, A, and on two rows. A map built from the input
        // order would name them in the wrong places, which is the mistake this guards.
        PlacedItem At(string label, float x, float y) =>
            new(new PackableItem(label, new Vector2(10, 10), 5), x, y);

        return new PackedPlate(
            1,
            [At("3705-0.10mm", 5, 5), At("3705-0.05mm", 20, 5), At("3705-0.00mm", 5, 40)],
            new Vector2(30, 45));
    }

    [Theory]
    [InlineData(DisplayLanguage.English)]
    [InlineData(DisplayLanguage.Italian)]
    public void The_sheet_is_written_in_the_language_of_the_run(DisplayLanguage language)
    {
        var sheet = CalibrationNotes.Write(
            APlateWhereTheOrderChanged(), [], "A1", Strings.For(language));

        sheet.Should().Contain(Strings.For(language)[TextKey.CalibrationTitle]);
    }

    /// <summary>The print settings are in this sheet, because there is no second one.</summary>
    [Fact]
    public void The_print_settings_are_in_this_sheet() =>
        CalibrationNotes.Write(APlateWhereTheOrderChanged(), [], "A1", Strings.English)
            .Should().Contain(PrintNotes.Settings(Strings.English));

    /// <summary>
    /// The map follows the placement, not the order the pieces were handed over.
    /// </summary>
    /// <remarks>
    /// The packer sorts by depth, then width, then label. Clearance changes a footprint, so the
    /// depths differ by step and the sort is not the input order. A map that assumed the input
    /// order would send someone to the wrong piece, and they would measure it and believe it.
    /// </remarks>
    [Fact]
    public void The_map_follows_where_the_packer_actually_put_things()
    {
        var sheet = CalibrationNotes.Write(
            APlateWhereTheOrderChanged(), [], "A1", Strings.English);

        var first = sheet.IndexOf("3705-0.10mm", StringComparison.Ordinal);
        var second = sheet.IndexOf("3705-0.05mm", StringComparison.Ordinal);
        var third = sheet.IndexOf("3705-0.00mm", StringComparison.Ordinal);

        first.Should().BeGreaterThan(0);
        first.Should().BeLessThan(second, "they share a row and 0.10 is to the left");
        second.Should().BeLessThan(third, "0.00 is on the row behind");
    }

    /// <summary>A part that could not be built is named, with the fit it took away.</summary>
    [Fact]
    public void A_missing_part_is_named_on_the_sheet() =>
        CalibrationNotes.Write(APlateWhereTheOrderChanged(), ["3673"], "A1", Strings.English)
            .Should().Contain("3673");

    /// <summary>The line to run once a row has been chosen is on the sheet, ready to copy.</summary>
    [Fact]
    public void The_command_to_save_the_answer_is_on_the_sheet() =>
        CalibrationNotes.Write(APlateWhereTheOrderChanged(), [], "A1", Strings.English)
            .Should().Contain("--save").And.Contain("--name");
}
```

- [ ] **Step 2: Run them and watch them fail**

```
dotnet test tests/Lego2STL.Tests/Lego2STL.Tests.csproj -f net10.0-windows10.0.19041.0 --filter "FullyQualifiedName~CalibrationNotes"
```

Expected: FAIL to compile — `CalibrationNotes` does not exist.

- [ ] **Step 3: Add the wording**

In `TextKey.cs`:

```csharp
    CalibrationMap,
    CalibrationWitness,
    CalibrationMissing,
    CalibrationSaveIt,
```

In `Strings.English.cs`:

```csharp
            [TextKey.CalibrationMap] =
                "Which piece is which, by where it sits. Rows run from the front of the bed "
                + "backwards, and each row from left to right. Nothing is marked on the pieces "
                + "themselves: engraving a number would change the very surface being measured.",
            [TextKey.CalibrationWitness] =
                "One piece carries no clearance at all: the wide plate. It is not part of the "
                + "measurement - it is there to be looked at. If its corners have lifted off the "
                + "bed, nothing else on this plate means anything yet, and the bed, the first "
                + "layer and the temperature come first.",
            [TextKey.CalibrationMissing] =
                "Not on the plate, because the library had no shape for them: {0}. Whatever fit "
                + "they tested went untested; the rest of the plate is unaffected.",
            [TextKey.CalibrationSaveIt] =
                "Then keep it. Run this, with your own name for it, and every build afterwards "
                + "can ask for it by that name:\n\n"
                + "    lego2stl calibration --save 0.15 --name \"eSUN PLA+ black - A1\" --preferred\n\n"
                + "--preferred makes it the one a build uses when none is asked for. Drop it if "
                + "you would rather name it every time with --tolerances.",
```

In `Strings.Italian.cs`:

```csharp
            [TextKey.CalibrationMap] =
                "Quale pezzo è quale, in base a dove si trova. Le file vanno dal davanti del "
                + "piano all'indietro, e ogni fila da sinistra a destra. Sui pezzi non c'è scritto "
                + "nulla: incidere un numero cambierebbe proprio la superficie da misurare.",
            [TextKey.CalibrationWitness] =
                "Un pezzo non ha alcuna tolleranza: la piastra larga. Non fa parte della misura, "
                + "sta lì per essere guardata. Se i suoi angoli si sono sollevati dal piano, nulla "
                + "di ciò che sta su questo piatto significa ancora qualcosa, e vengono prima il "
                + "piano, il primo strato e la temperatura.",
            [TextKey.CalibrationMissing] =
                "Non sono sul piatto, perché la libreria non aveva una forma per loro: {0}. "
                + "Qualunque accoppiamento provassero è rimasto non provato; il resto del piatto "
                + "non ne risente.",
            [TextKey.CalibrationSaveIt] =
                "Poi conservala. Esegui questo, con il nome che preferisci, e ogni build "
                + "successiva potrà richiederla con quel nome:\n\n"
                + "    lego2stl calibration --save 0.15 --name \"eSUN PLA+ nero - A1\" --preferred\n\n"
                + "--preferred la rende quella che una build usa quando non se ne chiede una. "
                + "Toglilo se preferisci nominarla ogni volta con --tolerances.",
```

- [ ] **Step 4: Write the sheet**

Create `src/Lego2STL.Core/Plates/CalibrationNotes.cs`:

```csharp
using System.Globalization;
using System.Text;
using Lego2STL.Core.Text;

namespace Lego2STL.Core.Plates;

/// <summary>
/// The single sheet beside a calibration plate.
/// </summary>
/// <remarks>
/// <para>
/// One sheet and not two. A build plate's folder gets how-to-print.txt beside its preset; a
/// calibration folder keeps its own sheet and that sheet carries the print settings as well,
/// because leaving two overlapping instruction files in one folder is the confusion the note was
/// written to prevent in the first place.
/// </para>
/// <para>
/// The map is built from where the packer actually put things. The packer sorts by depth, then
/// width, then label, and a clearance changes a footprint - so the order the pieces were handed
/// over is not the order they sit in, and a map that assumed it would send someone to the wrong
/// piece, which they would then measure and believe.
/// </para>
/// </remarks>
public static class CalibrationNotes
{
    /// <summary>Placements within this many millimetres of each other count as one row.</summary>
    private const float SameRow = 1f;

    public static string Write(
        PackedPlate plate,
        IReadOnlyList<string> missing,
        string? printer,
        Strings words)
    {
        ArgumentNullException.ThrowIfNull(plate);
        ArgumentNullException.ThrowIfNull(missing);
        ArgumentNullException.ThrowIfNull(words);

        var sheet = new StringBuilder();

        sheet.AppendLine(words[TextKey.CalibrationTitle]);
        sheet.AppendLine(new string('-', 70)).AppendLine();
        sheet.AppendLine(words[TextKey.CalibrationHow2]).AppendLine();

        if (missing.Count > 0)
        {
            sheet.AppendLine(words.Format(TextKey.CalibrationMissing, string.Join(", ", missing)));
            sheet.AppendLine();
        }

        sheet.AppendLine(words[TextKey.CalibrationMap]).AppendLine();
        AppendMap(sheet, plate);

        sheet.AppendLine().AppendLine(words[TextKey.CalibrationWitness]).AppendLine();
        sheet.AppendLine(words[TextKey.CalibrationThen]).AppendLine();
        sheet.AppendLine(words[TextKey.CalibrationSaveIt]).AppendLine();

        sheet.AppendLine(PrintNotes.Settings(words));

        if (ProcessPreset.BaseFor(printer) is not null)
        {
            sheet.AppendLine(words.Format(TextKey.PrintNotesImport, "Lego2STL.json"));
        }

        return sheet.ToString();
    }

    /// <summary>Rows from the front of the bed backwards, each row left to right.</summary>
    private static void AppendMap(StringBuilder sheet, PackedPlate plate)
    {
        var rows = plate.Items
            .OrderBy(i => i.Y)
            .ThenBy(i => i.X)
            .GroupBy(i => MathF.Round(i.Y / SameRow))
            .OrderBy(g => g.Key);

        var number = 1;

        foreach (var row in rows)
        {
            sheet.Append(string.Create(CultureInfo.InvariantCulture, $"  {number,2}. "));
            sheet.AppendLine(string.Join(
                "   ",
                row.OrderBy(i => i.X).Select(i => i.Item.PartNumber)));
            number++;
        }
    }
}
```

Note `TextKey.CalibrationHow2` above: the existing `CalibrationHow` takes two placeholders naming
the parts and the steps, which no longer suits a sheet that carries a map. Add a new key
`CalibrationHow2` with no placeholders — English *"Print the whole plate in the material you mean
to use, on the machine you mean to use, with the settings below. Nothing here can be carried over
from someone else's printer: the figure being looked for is smaller than the difference between two
machines of the same model."*, Italian *"Stampa tutto il piatto nel materiale che intendi usare,
sulla macchina che intendi usare, con le impostazioni qui sotto. Nulla di tutto questo può essere
ripreso dalla stampante di qualcun altro: la cifra che si cerca è più piccola della differenza fra
due macchine dello stesso modello."* Leave `CalibrationHow` in place; Task 10 removes its last
caller and you may delete it then if nothing else uses it.

- [ ] **Step 5: Run the whole suite**

```
dotnet test Lego2STL.slnx
```

Expected: PASS except `OptionParityTests`. If `The_map_follows_where_the_packer_actually_put_things`
fails, the grouping is using the input order somewhere — every read must come from `plate.Items`.

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "feat: the calibration sheet says which piece is where and how to keep the answer"
```

---

### Task 10: The calibration command builds the plate

**Files:**
- Create: `src/Lego2STL.Core/Plates/CalibrationRun.cs`
- Modify: `src/Lego2STL.Cli/Commands/CalibrationCommand.cs` (the building half of `RunAsync`, and
  `WriteInstructionsAsync`, which is replaced)
- Modify: `src/Lego2STL.Core/Text/TextKey.cs`, `Strings.English.cs`, `Strings.Italian.cs`
- Test: `tests/Lego2STL.UiTests/CalibrationPlateTests.cs` (create)

**Interfaces:**
- Consumes: `CalibrationSet.Items` and `.PartNumbers` (Task 8), `PlateWriter.WritePlatesAsync`
  (Task 1), `CalibrationNotes.Write` (Task 9), `ProcessPreset.For`, `PrintBeds.TryGetByName`.
- Produces: `sealed record CalibrationResult(int PieceCount, IReadOnlyList<string> Missing)` and
  `static Task<CalibrationResult> CalibrationRun.WriteAsync(IReadOnlyDictionary<string, PartMesh> sources, IReadOnlyList<double> steps, string printer, string directory, Strings words, CancellationToken cancellationToken = default)`;
  `--printer` on `calibration`. The folder then holds one `.3mf`, one `how-to-use-these.txt` and,
  when the printer is known, one `Lego2STL.json`.

The building lives in Core rather than in the command because Task 12 puts a button on it, and two
callers that each assemble the plate would be two plates that agree only for as long as someone
keeps them in step.

- [ ] **Step 1: Write the failing test**

Create `tests/Lego2STL.UiTests/CalibrationPlateTests.cs`:

```csharp
using System;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;

namespace Lego2STL.UiTests;

/// <summary>
/// What the building half of the calibration command does.
/// </summary>
/// <remarks>
/// <para>
/// One file to open and print, rather than one file per part per clearance to arrange by hand -
/// by a tool whose every other output is a packed plate.
/// </para>
/// <para>
/// Deliberately never fetches a real part. No test in this repository builds from the real LDraw
/// library: every one of them uses the fake, because the real one is a download and a test that
/// depends on one passes or fails by what is on the machine. What the set produces is covered
/// against fakes by CalibrationSetTests, what the sheet says by CalibrationNotesTests, and what
/// the whole thing looks like against real parts is the by-hand step at the end of this task.
/// </para>
/// </remarks>
public sealed class CalibrationPlateTests : IDisposable
{
    private readonly string _output = Path.Combine(
        Path.GetTempPath(), "lego2stl-calplate-" + Guid.NewGuid().ToString("N"));

    private readonly string _emptyLibrary = Path.Combine(
        Path.GetTempPath(), "lego2stl-nolibrary-" + Guid.NewGuid().ToString("N"));

    public CalibrationPlateTests() => Directory.CreateDirectory(_emptyLibrary);

    public void Dispose()
    {
        foreach (var folder in new[] { _output, _emptyLibrary })
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }
    }

    /// <summary>
    /// With no library to build from, it says so and leaves nothing behind.
    /// </summary>
    /// <remarks>
    /// The one end-to-end claim that can be made without a download, and it is worth making: it
    /// proves --printer and --output-dir are wired, that the command reaches the point of asking
    /// the library for parts, and that a failure there is a message rather than a half-written
    /// folder.
    /// </remarks>
    [Fact]
    public async Task With_nothing_to_build_from_it_says_so_and_writes_no_shapes()
    {
        var exit = await CalibrationManagementTests.RunAsync(
            "calibration",
            "--printer", "A1",
            "--offline",
            "--ldraw-dir", _emptyLibrary,
            "--output-dir", _output);

        exit.Should().NotBe(0);

        if (Directory.Exists(_output))
        {
            Directory.GetFiles(_output, "*.stl").Should().BeEmpty();
            Directory.GetFiles(_output, "*.3mf").Should().BeEmpty();
        }
    }

    /// <summary>
    /// The loose STLs are gone from the command's vocabulary entirely.
    /// </summary>
    /// <remarks>
    /// --part came off because the set is the set: substituting a part would leave the sheet
    /// describing a plate that was not built.
    /// </remarks>
    [Fact]
    public void The_command_no_longer_offers_to_substitute_a_part()
    {
        var command = Lego2STL.Cli.Commands.CalibrationCommand.Create(Lego2STL.Core.Text.Strings.English);

        command.Options.Should().NotContain(o => o.Name == "--part");
        command.Options.Should().Contain(o => o.Name == "--printer");
    }
}
```

`CalibrationManagementTests.RunAsync` was declared `internal static` in Task 7 for exactly this.

- [ ] **Step 2: Run it and watch it fail**

```
dotnet test tests/Lego2STL.UiTests/Lego2STL.UiTests.csproj -f net10.0-windows10.0.19041.0 --filter "FullyQualifiedName~CalibrationPlate"
```

Expected: FAIL — `--printer` is not a known option on `calibration`, and `--part` still is.

- [ ] **Step 3: Add `--printer`**

In `CalibrationCommand.Create`, declaring its own rather than reusing `PipelineOptions`, since
`calibration` takes none of the rest of that set:

```csharp
        var printer = new Option<string>("--printer")
        {
            Description = words.Format(TextKey.HelpOptPrinter, string.Join(", ", PrintBeds.Names)),
            DefaultValueFactory = _ => PrintBeds.Default.Name,
        };
```

Register it and pass its value into `RunAsync`.

- [ ] **Step 4: Put the building in Core**

Create `src/Lego2STL.Core/Plates/CalibrationRun.cs`:

```csharp
using System.Numerics;
using System.Text;
using Lego2STL.Core.Colors;
using Lego2STL.Core.Geometry;
using Lego2STL.Core.LDraw;
using Lego2STL.Core.Text;

namespace Lego2STL.Core.Plates;

/// <summary>What a calibration produced, and what it could not.</summary>
public sealed record CalibrationResult(int PieceCount, IReadOnlyList<string> Missing);

/// <summary>
/// Builds a calibration plate and everything that goes beside it, into one folder.
/// </summary>
/// <remarks>
/// In Core rather than in the command, because the window offers the same thing on a button and
/// two callers each assembling the plate would be two plates that agree only for as long as
/// someone keeps them in step.
/// </remarks>
public static class CalibrationRun
{
    /// <summary>The grey a plate of test pieces is written in; nothing here is a real colour.</summary>
    private static Rgb24 Neutral => Rgb24.Parse("#C8C8C8");

    public static async Task<CalibrationResult> WriteAsync(
        IReadOnlyDictionary<string, PartMesh> sources,
        IReadOnlyList<double> steps,
        string printer,
        string directory,
        Strings words,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sources);
        ArgumentNullException.ThrowIfNull(words);
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);

        var (items, missing) = CalibrationSet.Items(sources, steps, new MeshPipelineOptions());

        if (items.Count == 0)
        {
            return new CalibrationResult(0, missing);
        }

        var packing = new PackingOptions
        {
            Bed = PrintBeds.TryGetByName(printer, out var bed) ? bed : PrintBeds.Default,
        };

        var written = await PlateWriter
            .WritePlatesAsync(
                items,
                "calibration",
                words[TextKey.CalibrationTitle],
                Neutral,
                directory,
                packing,
                cancellationToken)
            .ConfigureAwait(false);

        if (ProcessPreset.For(printer) is { } preset)
        {
            await File.WriteAllTextAsync(
                    Path.Combine(directory, "Lego2STL.json"), preset, cancellationToken)
                .ConfigureAwait(false);
        }

        // The packer is asked where things went rather than told: the sheet's map has to be true,
        // and the order the pieces were handed over is not the order they sit in.
        var packed = ShelfPacker.Pack(
            [.. items.SelectMany(i => Enumerable.Repeat(
                new PackableItem(i.Label, Footprint(i.Mesh), Height(i.Mesh)), i.Quantity))],
            packing);

        await File.WriteAllTextAsync(
                Path.Combine(directory, "how-to-use-these.txt"),
                CalibrationNotes.Write(
                    packed.Plates.Count > 0 ? packed.Plates[0] : new PackedPlate(1, [], Vector2.Zero),
                    missing,
                    printer,
                    words),
                new UTF8Encoding(true),
                cancellationToken)
            .ConfigureAwait(false);

        return new CalibrationResult(written.Plates.Sum(p => p.PieceCount), missing);
    }

    private static Vector2 Footprint(IndexedMesh mesh)
    {
        var (min, max) = mesh.Bounds();
        return new Vector2(max.X - min.X, max.Y - min.Y);
    }

    private static float Height(IndexedMesh mesh)
    {
        var (min, max) = mesh.Bounds();
        return max.Z - min.Z;
    }
}
```

**Packing twice is deliberate and the comment must stay:** `PlateWriter` packs to write the file,
and the sheet needs the same placement to describe it. Passing the placement back out of
`PlateWriter` would widen its result type for one caller; packing again is pure, deterministic and
cheap.

- [ ] **Step 5: Make the command a thin caller**

Replace the body of `RunAsync` after the management branch — everything from the `foreach (var
partNumber in parts)` loop to the end — with:

```csharp
        // A part the library has not got is left off rather than stopping a plate whose value is
        // mostly still there.
        var sources = new Dictionary<string, PartMesh>(StringComparer.OrdinalIgnoreCase);

        foreach (var partNumber in CalibrationSet.PartNumbers)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                sources[partNumber] = await builder.BuildAsync(partNumber, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (LDrawPartNotFoundException)
            {
                // Named on the sheet by the run itself, from what it was not given.
            }
        }

        var result = await CalibrationRun
            .WriteAsync(sources, steps, printer, directory, words, cancellationToken)
            .ConfigureAwait(false);

        if (result.PieceCount == 0)
        {
            Console.Error.WriteLine(
                $"{words[TextKey.MsgError]}: {words[TextKey.ErrCalibrationNothingToBuild]}");
            return Program.ExitFailure;
        }

        Console.WriteLine();
        Console.WriteLine(words.Format(TextKey.MsgCalibrationWritten, result.PieceCount, directory));

        return Program.ExitOk;
```

Then delete `WriteInstructionsAsync`, delete `DefaultParts`, move `DefaultSteps` off this class (it
is now `CalibrationSet.DefaultSteps`, from Task 8) and have `ParseSteps` fall back to that, and
remove `--part` from the command: the set is the set, and letting someone substitute one part for
another would leave a sheet describing a plate that was not built. Keep `--steps`.

The output directory must now be created **after** the management branch and only when something is
about to be built, or `Listing_builds_nothing` from Task 7 fails.

- [ ] **Step 6: Add the two new keys**

In `TextKey.cs`: `CalibrationHow2` (from Task 9, if not already added) and
`ErrCalibrationNothingToBuild`. English: *"Nothing could be built: the shape library had none of
the calibration parts."* Italian: *"Non è stato possibile costruire nulla: la libreria delle forme
non aveva nessuno dei pezzi di calibrazione."*

Also remove `--part`'s `TextKey.HelpOptPart` usage from this command; leave the key itself if
another command uses it, and delete it if none does.

- [ ] **Step 7: Run the whole suite**

```
dotnet test Lego2STL.slnx
```

Expected: PASS except `OptionParityTests`. If the plate comes out as several files rather than one,
check the bed: `--printer` must reach `PackingOptions.Bed`, or everything is being packed onto the
default.

- [ ] **Step 8: Try it for real**

```
dotnet run --project src/Lego2STL.Cli -f net10.0-windows10.0.19041.0 -- calibration --printer A1 --lang it --output-dir <scratchpad>/cal
```

Open the sheet and read it as someone who has never used the tool. Check by eye that the map's rows
match what a slicer shows when the `.3mf` is opened. **Stop here and hand the plate over** — whether
the map is usable next to a physical plate is a person's judgement, not a test's.

- [ ] **Step 9: Commit**

```bash
git add -A
git commit -m "feat: a calibration is one plate to print, not a folder to arrange"
```

---

### Task 11: The window remembers tolerances too

**Files:**
- Modify: `src/Lego2STL.Gui/ViewModels/SettingsViewModel.cs`
- Create: `src/Lego2STL.Gui/ViewModels/ToleranceRowViewModel.cs`
- Modify: `src/Lego2STL.Gui/Views/SettingsView.axaml`
- Modify: `src/Lego2STL.Core/Text/TextKey.cs`, `Strings.English.cs`, `Strings.Italian.cs`
- Test: `tests/Lego2STL.UiTests/SettingsTests.cs` (add to it)

**Interfaces:**
- Consumes: `TolerancePresets` (Task 3).
- Produces: `SettingsViewModel.ToleranceRows` (`ObservableCollection<ToleranceRowViewModel>`),
  `AddTolerance`, `RemoveTolerance`, and `ToleranceRowViewModel` with `Name`, `Millimetres`,
  `IsPreferred`.

Follow `ShopRowViewModel` and `SettingsViewModel.FillShops` / `RememberShops` exactly — same shape
on screen. The one difference: this list persists through `TolerancePresets`, not through
`UserSettings`, because the command line has to read it and cannot see the window's file. Put that
one sentence in a comment on the remember method.

- [ ] **Step 1: Write the failing test**

Add to `tests/Lego2STL.UiTests/SettingsTests.cs`:

```csharp
    /// <summary>
    /// A tolerance saved in the window is the one the command line reads.
    /// </summary>
    /// <remarks>
    /// The shops list beside this one persists into the window's own preferences; this list
    /// cannot, because the command line does not reference the window's assembly and so cannot
    /// see that file. Same shape on screen, different file underneath.
    /// </remarks>
    [AvaloniaFact]
    public void A_tolerance_saved_in_the_window_is_the_one_the_command_line_reads()
    {
        var settings = ASettingsScreen(out _, out _);

        settings.AddToleranceCommand.Execute(null);
        var row = settings.ToleranceRows[^1];
        row.Name = "eSUN PLA+ black - A1";
        row.Millimetres = 0.15;
        row.IsPreferred = true;

        TolerancePresets.Load().Should()
            .ContainSingle(p => p.Name == "eSUN PLA+ black - A1" && p.Millimetres == 0.15 && p.Preferred);
    }

    /// <summary>Only one row is preferred, however many are ticked.</summary>
    [AvaloniaFact]
    public void Ticking_a_second_tolerance_unticks_the_first()
    {
        var settings = ASettingsScreen(out _, out _);

        settings.AddToleranceCommand.Execute(null);
        settings.ToleranceRows[^1].Name = "first";
        settings.ToleranceRows[^1].IsPreferred = true;

        settings.AddToleranceCommand.Execute(null);
        settings.ToleranceRows[^1].Name = "second";
        settings.ToleranceRows[^1].IsPreferred = true;

        settings.ToleranceRows.Where(r => r.IsPreferred).Should().ContainSingle(r => r.Name == "second");
    }
```

`ASettingsScreen` already exists in that file. The UI suite already isolates app data through
`Isolation.cs`, so `TolerancePresets` will write to a temporary folder.

- [ ] **Step 2: Run them and watch them fail**

```
dotnet test tests/Lego2STL.UiTests/Lego2STL.UiTests.csproj -f net10.0-windows10.0.19041.0 --filter "FullyQualifiedName~tolerance"
```

Expected: FAIL to compile — `ToleranceRows` does not exist.

- [ ] **Step 3: Write the row**

Create `src/Lego2STL.Gui/ViewModels/ToleranceRowViewModel.cs`, the same shape as
`ShopRowViewModel` at the top of `SettingsViewModel.cs`:

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using Lego2STL.Core.Run;

namespace Lego2STL.Gui.ViewModels;

/// <summary>One measured clearance, as a row that can be edited.</summary>
public sealed partial class ToleranceRowViewModel : ObservableObject
{
    public ToleranceRowViewModel(TolerancePreset preset)
    {
        ArgumentNullException.ThrowIfNull(preset);

        Name = preset.Name;
        Millimetres = preset.Millimetres;
        IsPreferred = preset.Preferred;
    }

    [ObservableProperty]
    public partial string Name { get; set; }

    [ObservableProperty]
    public partial double Millimetres { get; set; }

    /// <summary>Whether a build with nothing else to go on takes its clearance from this.</summary>
    [ObservableProperty]
    public partial bool IsPreferred { get; set; }

    public TolerancePreset ToPreset() =>
        new(Name.Trim(), Millimetres, IsPreferred, DateTimeOffset.UtcNow);
}
```

- [ ] **Step 4: Fill and remember**

In `SettingsViewModel`, beside `FillShops` / `Add` / `RememberShops`, add the same three for
tolerances, and call `FillTolerances()` from the constructor where `FillShops()` is called:

```csharp
    /// <summary>The clearances measured so far, as rows that can be edited.</summary>
    public ObservableCollection<ToleranceRowViewModel> ToleranceRows { get; } = [];

    private void FillTolerances()
    {
        foreach (var preset in TolerancePresets.Load())
        {
            AddRow(new ToleranceRowViewModel(preset));
        }
    }

    private void AddRow(ToleranceRowViewModel row)
    {
        row.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ToleranceRowViewModel.IsPreferred) && row.IsPreferred)
            {
                foreach (var other in ToleranceRows.Where(r => r != row))
                {
                    other.IsPreferred = false;
                }
            }

            RememberTolerances();
        };

        ToleranceRows.Add(row);
    }

    /// <summary>
    /// Written to Core's own file rather than the window's preferences, because the command line
    /// has to read the same list and cannot see the window's assembly.
    /// </summary>
    private void RememberTolerances() =>
        TolerancePresets.Save(
        [
            .. ToleranceRows
                .Where(r => !string.IsNullOrWhiteSpace(r.Name))
                .Select(r => r.ToPreset()),
        ]);

    [RelayCommand]
    private void AddTolerance() =>
        AddRow(new ToleranceRowViewModel(
            new TolerancePreset(string.Empty, 0.15, Preferred: false, DateTimeOffset.UtcNow)));

    /// <summary>
    /// Unlike the shops, none has to be preferred: none means a build applies no clearance, which
    /// is the state this tool starts in and is entitled to stay in.
    /// </summary>
    [RelayCommand]
    private void RemoveTolerance(ToleranceRowViewModel? row)
    {
        if (row is not null && ToleranceRows.Remove(row))
        {
            RememberTolerances();
        }
    }
```

Note the difference from `FillShops`: that one calls `RememberShops()` at the end to write the
defaults out on first run. This one must **not**, because there are no default tolerances and
writing an empty list on every start is pointless churn.

- [ ] **Step 5: Put it on the screen**

In `SettingsView.axaml`, add the list below the shops list, following that list's markup exactly:
a heading through `TextKey.UiTolerances`, an `ItemsControl` over `ToleranceRows` with a text box for
the name, a numeric entry for the millimetres, a radio or check for preferred, and a remove button;
then an add button. Add `UiTolerances`, `UiToleranceName`, `UiToleranceMillimetres`,
`UiAddTolerance` and `UiRemoveTolerance` to `TextKey` and both language files — English
*"Tolerances"*, *"Name"*, *"Clearance (mm)"*, *"Add a tolerance"*, *"Remove"*; Italian
*"Tolleranze"*, *"Nome"*, *"Tolleranza (mm)"*, *"Aggiungi una tolleranza"*, *"Rimuovi"*.

- [ ] **Step 6: Run the whole suite**

```
dotnet test Lego2STL.slnx
```

Expected: PASS except `OptionParityTests`. `SetupTests.The_screen_draws_in_either_language` walks
every `TextBlock` looking for a label showing a raw key name, so a key you forgot to add to a
language will surface there.

- [ ] **Step 7: Commit**

```bash
git add -A
git commit -m "feat: the window keeps the clearances you measured"
```

---

### Task 12: The window uses them, and offers a build's two files

**Files:**
- Modify: `src/Lego2STL.Gui/ViewModels/RunOptionsViewModel.cs:87` (`Clearance`), `:239`
- Modify: `src/Lego2STL.Gui/ViewModels/OptionRowsViewModel.cs:154` (beside the `--clearance` row)
- Modify: `src/Lego2STL.Gui/ViewModels/RunDocumentViewModel.cs`
- Modify: `src/Lego2STL.Gui/Views/RunDocumentView.axaml`
- Modify: `src/Lego2STL.Gui/ViewModels/SettingsViewModel.cs` (the calibration button)
- Modify: `src/Lego2STL.Core/Text/TextKey.cs`, `Strings.English.cs`, `Strings.Italian.cs`
- Test: `tests/Lego2STL.UiTests/RunDocumentViewTests.cs`, `tests/Lego2STL.UiTests/SettingsTests.cs`

**Interfaces:**
- Consumes: `ClearanceChoice.Resolve` (Task 4), `TolerancePresets.Load` (Task 3),
  `RunLayout.PresetPath` and `.PrintNotesPath` (from A+B), `Desktop.Open`.
- Produces: `RunOptionsViewModel.Tolerances` (`string?`); `RunDocumentViewModel.OpenPrintNotes` and
  `OpenPreset` commands with `HasPrintNotes` / `HasPreset`; `SettingsViewModel.RunCalibration`.

This task turns `OptionParityTests` green again. It has been red since Task 6, on purpose.

- [ ] **Step 1: Write the failing tests**

Add to `tests/Lego2STL.UiTests/RunDocumentViewTests.cs`:

```csharp
    /// <summary>
    /// The window offers the sheet and the preset, the way it offers the parts list.
    /// </summary>
    /// <remarks>
    /// The item the previous sub-project's design left under "left for the plan" and never
    /// answered. Without it those two files exist in the folder and nobody using the window ever
    /// finds them.
    /// </remarks>
    [AvaloniaFact]
    public void A_run_offers_how_to_print_its_plates()
    {
        var layout = RunLayout.At(ARunFolder());
        Directory.CreateDirectory(layout.PlateDirectory);
        File.WriteAllText(layout.PrintNotesPath, "how to print");
        File.WriteAllText(layout.PresetPath, "{}");

        using var page = RunDocumentViewModel.Reopened(RunFolder.Read(layout.Root));

        page.HasPrintNotes.Should().BeTrue();
        page.HasPreset.Should().BeTrue();
    }

    /// <summary>A run from before this existed offers neither, and says nothing about it.</summary>
    [AvaloniaFact]
    public void A_run_without_them_offers_neither()
    {
        var layout = RunLayout.At(ARunFolder());

        using var page = RunDocumentViewModel.Reopened(RunFolder.Read(layout.Root));

        page.HasPrintNotes.Should().BeFalse();
        page.HasPreset.Should().BeFalse();
    }
```

Add to `tests/Lego2STL.UiTests/SettingsTests.cs`:

```csharp
    /// <summary>A tolerance chosen in the window reaches the settings a run would use.</summary>
    [AvaloniaFact]
    public void The_chosen_tolerance_reaches_the_settings_a_run_would_use()
    {
        TolerancePresets.Remember("black", 0.15, preferred: false);

        var settings = ASettingsScreen(out var options, out _);

        options.Tolerances = "black";

        options.ToSettings().Clearance.Should().Be(0.15);
        options.ToSettings().ClearanceFrom.Should().Be("black");
    }
```

- [ ] **Step 2: Run them and watch them fail**

```
dotnet test tests/Lego2STL.UiTests/Lego2STL.UiTests.csproj -f net10.0-windows10.0.19041.0
```

Expected: FAIL to compile — `HasPrintNotes` and `Tolerances` do not exist. `OptionParityTests` also
still fails on `--tolerances`.

- [ ] **Step 3: Carry the tolerance through the run options**

In `RunOptionsViewModel`, beside `Clearance`:

```csharp
    /// <summary>The saved tolerance to take the clearance from, when one was chosen.</summary>
    [ObservableProperty]
    public partial string? Tolerances { get; set; }
```

and in `ToSettings()` replace `Clearance = Clearance,` with a call to the one resolver:

```csharp
        // The same resolver the command line uses, so the two cannot come to disagree about
        // whether a preferred tolerance applies.
        var clearance = ClearanceChoice.Resolve(
            Clearance > 0 ? Clearance : null, Tolerances, TolerancePresets.Load());
```

and then `Clearance = clearance.Millimetres, ClearanceFrom = clearance.FromPreset,`.

The window's `Clearance` stays a plain `double` with 0 meaning "not set", because a numeric box has
no third state; the command line keeps the sharper distinction that a nullable option gives it.
Note that in a comment on the property.

- [ ] **Step 4: Put a control on the screen**

The window's options are one declarative table — `OptionRowsViewModel.Build` — not markup, so this
is a single entry. Add it immediately after the `--clearance` row at `:154`:

```csharp
            new ChoiceOptionRow("--tolerances", TextKey.LabelOptTolerances, TextKey.HelpOptTolerances,
                () => o.Tolerances, v => o.Tolerances = v, null,
                [.. TolerancePresets.Load().Select(p => p.Name)])
            {
                // Nothing to choose from until something has been measured, and a chooser with no
                // choices on a screen full of controls is a puzzle rather than a feature.
                Enabled = () => TolerancePresets.Load().Count > 0,
            },
```

Add `TextKey.LabelOptTolerances` to both languages — English *"Saved tolerance"*, Italian
*"Tolleranza salvata"*. `HelpOptTolerances` already exists from Task 6.

`ChoiceOptionRow`'s choices are captured once when the table is built, so a tolerance saved on the
Settings screen appears in this list the next time the options screen is built rather than
instantly. That is acceptable and worth a one-line comment; making it live would mean pushing a
change notification from Core's store into a view model, which is more machinery than the case
deserves.

- [ ] **Step 5: Offer the two files**

In `RunDocumentViewModel`, beside `OpenPartsList`:

```csharp
    public bool HasPrintNotes => File.Exists(Document.PrintNotesPath);

    public bool HasPreset => File.Exists(Document.PresetPath);

    private void OpenPrintNotes() => Desktop.Open(Document.PrintNotesPath);

    private void OpenPreset() => Desktop.Open(Document.PresetPath);
```

`RunDocument` will need `PrintNotesPath` and `PresetPath` alongside its existing `PartsListPath`
and `StlDirectory`, taken from the layout in both of its projections. Add the two buttons to
`RunDocumentView.axaml` beside the parts-list button, bound to `HasPrintNotes` / `HasPreset` for
visibility, with `TextKey.UiOpenPrintNotes` and `TextKey.UiOpenPreset` — English *"How to print"*,
*"Slicer preset"*; Italian *"Come stampare"*, *"Preset slicer"*.

- [ ] **Step 6: The calibration button**

`CalibrationRun.WriteAsync` already exists from Task 10 and is what the command calls, so the
button calls the same thing and the two cannot produce different plates. In `SettingsViewModel`:

```csharp
    /// <summary>
    /// Builds a calibration plate, because a calibration is run in order to fill the list above.
    /// </summary>
    [RelayCommand]
    private async Task RunCalibration()
    {
        var directory = Path.Combine(
            Options.OutputDirectory is { Length: > 0 } chosen ? chosen : Environment.CurrentDirectory,
            "calibration");

        using var library = new EscalatingLDrawLibrary(
            Options.ToSettings().LDrawOptions, _ => { }, Loc.Current.Words);

        var builder = new LDrawMeshBuilder(library);
        var sources = new Dictionary<string, PartMesh>(StringComparer.OrdinalIgnoreCase);

        foreach (var partNumber in CalibrationSet.PartNumbers)
        {
            try
            {
                sources[partNumber] = await builder.BuildAsync(partNumber).ConfigureAwait(true);
            }
            catch (LDrawPartNotFoundException)
            {
                // Left off the plate and named on the sheet, exactly as the command does.
            }
        }

        await CalibrationRun
            .WriteAsync(sources, CalibrationSet.DefaultSteps, Options.Printer, directory, Loc.Current.Words)
            .ConfigureAwait(true);

        Desktop.Open(directory);
    }
```

`CalibrationSet.DefaultSteps` comes from Task 8, so the six numbers are not duplicated here.

Label the button `TextKey.UiRunCalibration` — English *"Build a calibration plate"*, Italian
*"Costruisci un piatto di calibrazione"* — and add it to `SettingsView.axaml` under the tolerance
list, following the markup of the existing "add a shop" button.

`[RelayCommand]` on an `async Task` method generates `RunCalibrationCommand` as an async command,
which is what the button binds to. Keep the method under 50 lines; it is at about 30 as written.

- [ ] **Step 7: Run the whole suite**

```
dotnet test Lego2STL.slnx
```

Expected: **PASS, all of it, `OptionParityTests` included.** This is the first task since Task 6
where the suite is fully green; if `OptionParityTests` still fails, the window has no control bound
for `--tolerances`.

- [ ] **Step 8: Commit**

```bash
git add -A
git commit -m "feat: the window uses a saved tolerance and offers a run's printing files"
```

---

## Notes for whoever executes this

- **Task 1 is a refactor and `PlateBuilderTests` is its whole proof.** If you find yourself editing
  one of its assertions, stop: the extraction changed behaviour. The usual cause is the file stem,
  which must stay the translated colour name.
- **The suite is deliberately red between Task 6 and Task 12**, on `OptionParityTests` only, because
  that test asks the real command-line declaration what it accepts and demands the window match.
  Do not weaken it to get past the middle of the plan. Every other test must stay green throughout.
- **Task 10 Step 7 stops for a person.** Whether the sheet's map is usable next to a physical plate
  is a judgement no test makes.
- The `--part` option comes off `calibration` in Task 10. That is deliberate: the set is the set,
  and substituting a part would leave the sheet describing a plate that was not built.
- Record `PHASE:C WAVE:<n> STATUS:complete TS:<ISO-8601-UTC>` in `PROGRESS.md` after each task, and
  `PHASE:C WAVE:0 STATUS:complete` when all twelve are done.
