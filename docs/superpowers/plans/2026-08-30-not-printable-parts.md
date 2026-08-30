# Parts That Are Not Printed Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Stop the run building parts nobody can print — rubber, cloth, card, foam, flexible
plastic, metal, electronics and stickers — show a picture of each in the catalogue, and offer to
buy it from a shop the user chose.

**Architecture:** Three strands. Strand 1 teaches the existing dump reader to answer what a part
is made of and what kind of thing it is, adds one rule that turns those two facts into a verdict,
and has the run ask before it builds. Strand 2 gives a part with no shape a picture: cropped from
the document it was read from, or fetched from Rebrickable when there is no document. Strand 3
adds an editable list of shops to the settings and a Buy button to the card.

**Tech Stack:** C# / .NET 10, xUnit + FluentAssertions, Avalonia 12.1.1 (headless for UI tests),
CommunityToolkit.Mvvm, SkiaSharp.

**Spec:** `docs/superpowers/specs/2026-08-30-not-printable-parts-design.md`

## Global Constraints

- Build with `dotnet build Lego2STL.slnx -c Debug`. Test with `dotnet test Lego2STL.slnx`.
  Two test projects: `Lego2STL.Tests` (core) and `Lego2STL.UiTests` (Avalonia headless,
  `[AvaloniaFact]`).
- Every user-facing string goes through `TextKey` and is added to **both**
  `Strings.English.cs` and `Strings.Italian.cs`. `StringsTests` fails the build if a key is
  missing from either.
- Code comments and CHANGELOG entries: **one sentence each**. Test comments are exempt.
- Commit messages: `<type>: <description>`, describing observable behaviour, never internal
  class or method names.
- Files stay under 800 lines; functions under 50.
- The parts-list CSV keeps its six columns. Do not add a seventh.
- **The dump is optional and always has been.** No missing, unreadable or malformed file may
  fail a run, change its result, or throw. `RebrickableDump` already states this rule —
  *"optional input: never fail the run because of it"* — and every method added here obeys it.
- **An unknown part is never a reason to leave anything out.** A part the dump has not heard of
  is built exactly as it is today.
- Adding a CLI option makes `OptionParityTests` and `OptionRoundTripTests` fail until the window
  has the matching row. That is the tests working; add the row rather than weakening them.

---

### Task 1: The dump says what a part is made of and what kind it is

**Files:**
- Modify: `src/Lego2STL.Core/Rebrickable/RebrickableDump.cs`
- Test: `tests/Lego2STL.Tests/Catalogue/DumpPartFactsTests.cs` (create)

**Interfaces:**
- Consumes: nothing from earlier tasks.
- Produces: `public sealed record PartFact(string Category, string Material)` in
  `Lego2STL.Core.Rebrickable`; `RebrickableDump.TryReadPartFacts(params string?[] candidates)`
  returning `IReadOnlyDictionary<string, PartFact>` keyed case-insensitively by part number,
  empty when there is no readable dump; `RebrickableDump.TryFindFile(string? path, string fileName)`
  made public, with `TryFindElementsFile` delegating to it.

- [ ] **Step 1: Write the failing tests**

Create `tests/Lego2STL.Tests/Catalogue/DumpPartFactsTests.cs`:

```csharp
using FluentAssertions;
using Lego2STL.Core.Rebrickable;

namespace Lego2STL.Tests.Catalogue;

/// <summary>
/// Reading what the dump knows about a part, and staying quiet when it knows nothing.
/// </summary>
/// <remarks>
/// The dump is optional and is not ours to redistribute, so every test here builds its own
/// two-file fixture. The behaviour that matters most is the absence case: a run with no dump,
/// or with a dump missing the column, has to carry on exactly as it did before.
/// </remarks>
public sealed class DumpPartFactsTests : IDisposable
{
    private readonly string _folder = Path.Combine(
        Path.GetTempPath(), "lego2stl-dump-" + Guid.NewGuid().ToString("N"));

    public DumpPartFactsTests() => Directory.CreateDirectory(_folder);

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    private void WriteDump(string partsBody, string categoriesBody = "id,name\n65,Electronics\n27,Tubes and Hoses\n")
    {
        File.WriteAllText(Path.Combine(_folder, "parts.csv"), partsBody.ReplaceLineEndings());
        File.WriteAllText(Path.Combine(_folder, "part_categories.csv"), categoriesBody.ReplaceLineEndings());
    }

    [Fact]
    public void A_part_in_the_dump_brings_its_kind_and_its_material()
    {
        WriteDump("part_num,name,part_cat_id,part_material\n" +
                  "5102c13,\"Hose, Pneumatic 4mm D. 13L\",27,Rubber\n" +
                  "22127,Hub Powered Up,65,Plastic\n");

        var facts = RebrickableDump.TryReadPartFacts(_folder);

        facts["5102c13"].Should().Be(new PartFact("Tubes and Hoses", "Rubber"));
        facts["22127"].Should().Be(new PartFact("Electronics", "Plastic"));
    }

    /// <summary>Part numbers are matched however they were typed, as everywhere else.</summary>
    [Fact]
    public void A_part_is_found_whatever_case_it_is_asked_for_in()
    {
        WriteDump("part_num,name,part_cat_id,part_material\n4265C,Bush,27,Plastic\n");

        RebrickableDump.TryReadPartFacts(_folder).ContainsKey("4265c").Should().BeTrue();
    }

    [Fact]
    public void A_part_the_dump_has_never_heard_of_is_simply_absent()
    {
        WriteDump("part_num,name,part_cat_id,part_material\n3001,Brick 2x4,11,Plastic\n");

        RebrickableDump.TryReadPartFacts(_folder).ContainsKey("99999").Should().BeFalse();
    }

    [Fact]
    public void No_dump_at_all_is_no_facts_and_no_complaint()
    {
        RebrickableDump.TryReadPartFacts(Path.Combine(_folder, "nowhere")).Should().BeEmpty();
        RebrickableDump.TryReadPartFacts(null).Should().BeEmpty();
    }

    /// <summary>A dump whose shape has changed is treated as no dump, never as a reason to stop.</summary>
    [Fact]
    public void A_parts_file_without_the_columns_is_treated_as_no_dump()
    {
        WriteDump("part_num,name\n3001,Brick 2x4\n");

        RebrickableDump.TryReadPartFacts(_folder).Should().BeEmpty();
    }

    /// <summary>
    /// The setting may name one file of the dump rather than the folder, because that is what
    /// it was for before this: the others are found beside it.
    /// </summary>
    [Fact]
    public void Pointing_at_one_file_of_the_dump_finds_the_others_beside_it()
    {
        WriteDump("part_num,name,part_cat_id,part_material\n3001,Brick 2x4,11,Plastic\n",
            "id,name\n11,Bricks\n");
        var elements = Path.Combine(_folder, "elements.csv");
        File.WriteAllText(elements, "element_id,part_num,color_id\n300126,3001,4\n");

        RebrickableDump.TryReadPartFacts(elements).Should().ContainKey("3001");
    }

    /// <summary>The first candidate that answers wins, so the setting beats the working folder.</summary>
    [Fact]
    public void The_first_candidate_that_answers_is_the_one_used()
    {
        WriteDump("part_num,name,part_cat_id,part_material\n3001,Brick 2x4,11,Plastic\n",
            "id,name\n11,Bricks\n");

        RebrickableDump.TryReadPartFacts(null, "nowhere", _folder).Should().ContainKey("3001");
    }
}
```

- [ ] **Step 2: Run them and watch them fail**

```
dotnet test tests/Lego2STL.Tests/Lego2STL.Tests.csproj --filter "FullyQualifiedName~DumpPartFacts"
```

Expected: FAIL to compile — neither `PartFact` nor `TryReadPartFacts` exists.

- [ ] **Step 3: Generalise the file finder**

In `src/Lego2STL.Core/Rebrickable/RebrickableDump.cs`, replace the body of `TryFindElementsFile`
with a delegation, and add the general finder beside it:

```csharp
    /// <summary>
    /// The <c>elements.csv</c> a path points at, whether it names the file, the folder holding
    /// it, or a folder holding the folder.
    /// </summary>
    public static string? TryFindElementsFile(string? path) => TryFindFile(path, ElementsFileName);

    /// <summary>
    /// One named file of a dump, from a path that names it, the folder holding it, or a folder
    /// holding that folder.
    /// </summary>
    /// <remarks>
    /// A path naming one file of the dump names the folder for all the others, because the
    /// setting that carries it was written when only <c>elements.csv</c> was wanted.
    /// </remarks>
    public static string? TryFindFile(string? path, string fileName)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        if (File.Exists(path))
        {
            if (string.Equals(Path.GetFileName(path), fileName, StringComparison.OrdinalIgnoreCase))
            {
                return path;
            }

            var beside = Path.Combine(
                Path.GetDirectoryName(Path.GetFullPath(path)) ?? ".", fileName);

            return File.Exists(beside) ? beside : null;
        }

        if (!Directory.Exists(path))
        {
            return null;
        }

        var here = Path.Combine(path, fileName);
        if (File.Exists(here))
        {
            return here;
        }

        try
        {
            // One level down, so that a folder holding an unpacked dump next to the documents
            // is found without anyone having to name it.
            return Directory
                .EnumerateDirectories(path)
                .Select(d => Path.Combine(d, fileName))
                .FirstOrDefault(File.Exists);
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }
```

- [ ] **Step 4: Add the record and the reader**

In the same file, above `RebrickableDump`:

```csharp
/// <summary>What the dump says a part is: the kind of thing, and what it is made of.</summary>
public sealed record PartFact(string Category, string Material);
```

And inside `RebrickableDump`, beside `ElementsFileName`:

```csharp
    /// <summary>The file in a dump that lists every part.</summary>
    public const string PartsFileName = "parts.csv";

    /// <summary>The file in a dump that names the categories the parts file refers to.</summary>
    public const string CategoriesFileName = "part_categories.csv";

    /// <summary>
    /// Reads <c>parts.csv</c> and <c>part_categories.csv</c> into part number to kind and
    /// material.
    /// </summary>
    /// <param name="candidates">
    /// Places to look, best first: the setting, then the document's own folder, then wherever
    /// the command was run from. The first that answers is used.
    /// </param>
    /// <returns>An empty map when there is no readable dump. Never throws.</returns>
    public static IReadOnlyDictionary<string, PartFact> TryReadPartFacts(params string?[] candidates)
    {
        var empty = new Dictionary<string, PartFact>(StringComparer.OrdinalIgnoreCase);

        foreach (var candidate in candidates ?? [])
        {
            var facts = ReadPartFacts(candidate);
            if (facts.Count > 0)
            {
                return facts;
            }
        }

        return empty;
    }

    private static Dictionary<string, PartFact> ReadPartFacts(string? path)
    {
        var empty = new Dictionary<string, PartFact>(StringComparer.OrdinalIgnoreCase);

        var partsFile = TryFindFile(path, PartsFileName);
        if (partsFile is null)
        {
            return empty;
        }

        try
        {
            var categories = ReadCategoryNames(TryFindFile(path, CategoriesFileName));

            var lines = File.ReadAllLines(partsFile);
            if (lines.Length == 0)
            {
                return empty;
            }

            var header = SplitCsvLine(lines[0]);
            var numberIndex = IndexOf(header, "part_num");
            var categoryIndex = IndexOf(header, "part_cat_id");
            var materialIndex = IndexOf(header, "part_material");

            if (numberIndex < 0 || categoryIndex < 0 || materialIndex < 0)
            {
                return empty;
            }

            var facts = new Dictionary<string, PartFact>(StringComparer.OrdinalIgnoreCase);

            foreach (var line in lines.Skip(1))
            {
                if (line.Length == 0)
                {
                    continue;
                }

                var f = SplitCsvLine(line);
                if (f.Length <= Math.Max(numberIndex, Math.Max(categoryIndex, materialIndex)))
                {
                    continue;
                }

                facts[f[numberIndex]] = new PartFact(
                    categories.GetValueOrDefault(f[categoryIndex], string.Empty),
                    f[materialIndex]);
            }

            return facts;
        }
        catch (IOException)
        {
            return empty;   // optional input: never fail the run because of it
        }
        catch (UnauthorizedAccessException)
        {
            return empty;
        }
    }

    private static Dictionary<string, string> ReadCategoryNames(string? categoriesFile)
    {
        var names = new Dictionary<string, string>(StringComparer.Ordinal);
        if (categoriesFile is null)
        {
            return names;
        }

        try
        {
            var lines = File.ReadAllLines(categoriesFile);
            if (lines.Length == 0)
            {
                return names;
            }

            var header = SplitCsvLine(lines[0]);
            var idIndex = IndexOf(header, "id");
            var nameIndex = IndexOf(header, "name");

            if (idIndex < 0 || nameIndex < 0)
            {
                return names;
            }

            foreach (var line in lines.Skip(1))
            {
                var f = SplitCsvLine(line);
                if (f.Length > Math.Max(idIndex, nameIndex))
                {
                    names[f[idIndex]] = f[nameIndex];
                }
            }

            return names;
        }
        catch (IOException)
        {
            return names;
        }
        catch (UnauthorizedAccessException)
        {
            return names;
        }
    }
```

- [ ] **Step 5: Run the tests**

```
dotnet test tests/Lego2STL.Tests/Lego2STL.Tests.csproj --filter "FullyQualifiedName~DumpPartFacts"
```

Expected: PASS, all seven.

- [ ] **Step 6: Commit**

```bash
git add src/Lego2STL.Core/Rebrickable/RebrickableDump.cs tests/Lego2STL.Tests/Catalogue/DumpPartFactsTests.cs
git commit -m "feat: the parts database can be asked what a part is made of"
```

---

### Task 2: The rule that decides whether a part is printed

**Files:**
- Create: `src/Lego2STL.Core/Catalogue/Printability.cs`
- Test: `tests/Lego2STL.Tests/Catalogue/PrintabilityTests.cs` (create)

**Interfaces:**
- Consumes: `PartFact` from Task 1.
- Produces: `enum Printable { Yes, NotItsMaterial, NotItsKind, Unknown }` and
  `static class Printability` with `Of(PartFact?) → Printable`, `IsPrinted(this Printable) → bool`,
  `UnprintableMaterials` and `UnprintableCategories` as `IReadOnlySet<string>`,
  `Token(this Printable) → string` giving the kebab-case word the manifest stores
  (`"yes"`, `"material"`, `"kind"`, `"unknown"`), `FromToken(string?) → Printable`, and
  `Choose(IReadOnlyList<string> parts, IReadOnlyDictionary<string, PartFact> facts, bool printEverything)`
  returning `(IReadOnlyList<string> Build, IReadOnlyList<string> Leave)` — the split the run acts
  on, kept here so that it can be tested without a shape library or a network.

- [ ] **Step 1: Write the failing tests**

Create `tests/Lego2STL.Tests/Catalogue/PrintabilityTests.cs`:

```csharp
using FluentAssertions;
using Lego2STL.Core.Catalogue;
using Lego2STL.Core.Rebrickable;

namespace Lego2STL.Tests.Catalogue;

/// <summary>
/// Which parts a printer is asked to make.
/// </summary>
/// <remarks>
/// Measured on run 6324712: three pneumatic hoses are rubber and fail loudly, while a Powered Up
/// hub and two motors are plastic, succeed, and are printed as hollow shells of things that have
/// to be bought. Material alone cannot tell the second group apart - every one of the dump's 615
/// electronic parts is plastic - which is why the kind is consulted as well.
/// </remarks>
public sealed class PrintabilityTests
{
    [Theory]
    [InlineData("Tubes and Hoses", "Rubber", Printable.NotItsMaterial)]
    [InlineData("Gear Parts", "Cardboard/Paper", Printable.NotItsMaterial)]
    [InlineData("Minifig Upper Body", "Cloth", Printable.NotItsMaterial)]
    [InlineData("Technic Special", "Foam", Printable.NotItsMaterial)]
    [InlineData("Tubes and Hoses", "Flexible Plastic", Printable.NotItsMaterial)]
    [InlineData("Technic Special", "Metal", Printable.NotItsMaterial)]
    [InlineData("Electronics", "Plastic", Printable.NotItsKind)]
    [InlineData("Stickers", "Plastic", Printable.NotItsKind)]
    [InlineData("Technic Beams", "Plastic", Printable.Yes)]
    [InlineData("Bricks", "Plastic", Printable.Yes)]
    public void The_kind_and_the_material_together_decide(string category, string material, Printable expected)
    {
        Printability.Of(new PartFact(category, material)).Should().Be(expected);
    }

    /// <summary>
    /// A rubber tyre is reported as rubber rather than as a wheel, because the material is the
    /// more specific answer and the one a person can act on.
    /// </summary>
    [Fact]
    public void The_material_is_answered_before_the_kind()
    {
        Printability.Of(new PartFact("Electronics", "Rubber")).Should().Be(Printable.NotItsMaterial);
    }

    [Fact]
    public void A_part_the_dump_does_not_know_is_unknown_and_still_printed()
    {
        var verdict = Printability.Of(null);

        verdict.Should().Be(Printable.Unknown);
        verdict.IsPrinted().Should().BeTrue("an absence is never a reason to leave a part out");
    }

    [Fact]
    public void Only_the_two_refusals_stop_a_part_being_printed()
    {
        Printable.Yes.IsPrinted().Should().BeTrue();
        Printable.NotItsMaterial.IsPrinted().Should().BeFalse();
        Printable.NotItsKind.IsPrinted().Should().BeFalse();
    }

    /// <summary>The word the run's record keeps, which has to survive being written and read.</summary>
    [Theory]
    [InlineData(Printable.Yes, "yes")]
    [InlineData(Printable.NotItsMaterial, "material")]
    [InlineData(Printable.NotItsKind, "kind")]
    [InlineData(Printable.Unknown, "unknown")]
    public void Every_verdict_survives_a_trip_through_its_word(Printable verdict, string token)
    {
        verdict.Token().Should().Be(token);
        Printability.FromToken(token).Should().Be(verdict);
    }

    /// <summary>A record written before any of this says nothing, and nothing is what it means.</summary>
    [Fact]
    public void A_record_with_no_word_reads_as_unknown()
    {
        Printability.FromToken(null).Should().Be(Printable.Unknown);
        Printability.FromToken("something else entirely").Should().Be(Printable.Unknown);
    }

    private static readonly Dictionary<string, PartFact> AFewFacts = new(StringComparer.OrdinalIgnoreCase)
    {
        ["5102c13"] = new("Tubes and Hoses", "Rubber"),
        ["22127"] = new("Electronics", "Plastic"),
        ["32523"] = new("Technic Beams", "Plastic"),
    };

    [Fact]
    public void A_hose_and_a_hub_are_left_out_and_a_beam_is_not()
    {
        var (build, leave) = Printability.Choose(
            ["5102c13", "22127", "32523", "99999"], AFewFacts, printEverything: false);

        build.Should().Equal("32523", "99999");
        leave.Should().Equal("5102c13", "22127");
    }

    /// <summary>The order of the list is kept, so a run reads the way its list does.</summary>
    [Fact]
    public void The_two_sides_keep_the_order_the_list_had()
    {
        var (build, _) = Printability.Choose(
            ["32523", "99999", "3705"], AFewFacts, printEverything: false);

        build.Should().Equal("32523", "99999", "3705");
    }

    [Fact]
    public void With_no_facts_at_all_every_part_is_still_built()
    {
        var (build, leave) = Printability.Choose(
            ["5102c13", "22127", "32523"], new Dictionary<string, PartFact>(), printEverything: false);

        build.Should().Equal("5102c13", "22127", "32523");
        leave.Should().BeEmpty();
    }

    /// <summary>Asking for everything asks for everything, whatever the database says.</summary>
    [Fact]
    public void Print_everything_builds_the_hose_and_the_hub_too()
    {
        var (build, leave) = Printability.Choose(
            ["5102c13", "22127", "32523"], AFewFacts, printEverything: true);

        build.Should().Equal("5102c13", "22127", "32523");
        leave.Should().BeEmpty();
    }
}
```

- [ ] **Step 2: Run them and watch them fail**

```
dotnet test tests/Lego2STL.Tests/Lego2STL.Tests.csproj --filter "FullyQualifiedName~Printability"
```

Expected: FAIL to compile — `Printable` and `Printability` do not exist.

- [ ] **Step 3: Write it**

Create `src/Lego2STL.Core/Catalogue/Printability.cs`:

```csharp
using Lego2STL.Core.Rebrickable;

namespace Lego2STL.Core.Catalogue;

/// <summary>Whether a part is printed at all, and when it is not, why not.</summary>
public enum Printable
{
    /// <summary>Nothing known about it says otherwise.</summary>
    Yes,

    /// <summary>Made of something no printer can lay down.</summary>
    NotItsMaterial,

    /// <summary>A kind of thing that is bought rather than made: electronics, stickers.</summary>
    NotItsKind,

    /// <summary>Nothing is known about it, which is not a reason to leave it out.</summary>
    Unknown,
}

/// <summary>
/// Decides which parts a printer is asked to make.
/// </summary>
/// <remarks>
/// The kind has to be consulted as well as the material because every one of the parts database's
/// 615 electronic parts is plastic, battery boxes included - so a run reading the material alone
/// prints hollow shells of things that have to be bought.
/// </remarks>
public static class Printability
{
    /// <summary>Materials no printer can lay down.</summary>
    public static IReadOnlySet<string> UnprintableMaterials { get; } =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Rubber", "Cloth", "Cardboard/Paper", "Foam", "Flexible Plastic", "Metal",
        };

    /// <summary>Kinds of part that are bought rather than made, whatever they are made of.</summary>
    public static IReadOnlySet<string> UnprintableCategories { get; } =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Electronics", "Stickers" };

    /// <summary>The material is answered first, being the more specific of the two.</summary>
    public static Printable Of(PartFact? fact) =>
        fact is null ? Printable.Unknown
        : UnprintableMaterials.Contains(fact.Material) ? Printable.NotItsMaterial
        : UnprintableCategories.Contains(fact.Category) ? Printable.NotItsKind
        : Printable.Yes;

    /// <summary>True when the run should build it; an unknown part is built like any other.</summary>
    public static bool IsPrinted(this Printable verdict) =>
        verdict is Printable.Yes or Printable.Unknown;

    /// <summary>The word a run's record keeps, so the wording can be chosen when it is read.</summary>
    public static string Token(this Printable verdict) => verdict switch
    {
        Printable.NotItsMaterial => "material",
        Printable.NotItsKind => "kind",
        Printable.Unknown => "unknown",
        _ => "yes",
    };

    /// <summary>Reads that word back. Anything unrecognised, including nothing, is unknown.</summary>
    public static Printable FromToken(string? token) => token switch
    {
        "material" => Printable.NotItsMaterial,
        "kind" => Printable.NotItsKind,
        "yes" => Printable.Yes,
        _ => Printable.Unknown,
    };

    /// <summary>
    /// Splits a list into the parts to build and the parts to leave, keeping their order.
    /// </summary>
    /// <param name="printEverything">
    /// Build them all regardless, for anyone who wants the shell of an electronic part.
    /// </param>
    public static (IReadOnlyList<string> Build, IReadOnlyList<string> Leave) Choose(
        IReadOnlyList<string> parts,
        IReadOnlyDictionary<string, PartFact> facts,
        bool printEverything)
    {
        ArgumentNullException.ThrowIfNull(parts);
        ArgumentNullException.ThrowIfNull(facts);

        if (printEverything)
        {
            return (parts, []);
        }

        var build = new List<string>(parts.Count);
        var leave = new List<string>();

        foreach (var part in parts)
        {
            if (Of(facts.GetValueOrDefault(part)).IsPrinted())
            {
                build.Add(part);
            }
            else
            {
                leave.Add(part);
            }
        }

        return (build, leave);
    }
}
```

- [ ] **Step 4: Run the tests**

```
dotnet test tests/Lego2STL.Tests/Lego2STL.Tests.csproj --filter "FullyQualifiedName~Printability"
```

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Lego2STL.Core/Catalogue/Printability.cs tests/Lego2STL.Tests/Catalogue/PrintabilityTests.cs
git commit -m "feat: a part's kind and material decide whether it is printed"
```

---

### Task 3: The run does not build what cannot be printed

**Files:**
- Modify: `src/Lego2STL.Core/Pipeline/RunOutcome.cs`
- Modify: `src/Lego2STL.Core/Pipeline/RunSettings.cs`
- Modify: `src/Lego2STL.Core/Pipeline/PipelineRunner.cs:450-476` (`BuildShapesAsync`)
- Modify: `src/Lego2STL.Cli/Commands/PipelineOptions.cs`
- Modify: `src/Lego2STL.Gui/ViewModels/RunOptionsViewModel.cs`,
  `src/Lego2STL.Gui/ViewModels/OptionRowsViewModel.cs`
- Modify: `src/Lego2STL.Core/Text/TextKey.cs`, `Strings.English.cs`, `Strings.Italian.cs`
- Test: `tests/Lego2STL.Tests/Pipeline/NotPrintedPartsTests.cs` (create)

**Interfaces:**
- Consumes: `Printability.Of`, `Printable.IsPrinted`, `RebrickableDump.TryReadPartFacts`.
- Produces: `RunOutcome.PartFacts` (`IReadOnlyDictionary<string, PartFact>`, empty by default)
  and `RunOutcome.NotPrinted` (`IReadOnlyList<string>`, the part numbers left out, in the order
  the list holds them); `RunSettings.PrintEverything` (`bool`), CLI `--print-everything`.
  Task 4 records both on the manifest; Task 5 shows them.

- [ ] **Step 1: Write the failing test**

Create `tests/Lego2STL.Tests/Pipeline/NotPrintedPartsTests.cs`:

```csharp
using FluentAssertions;
using Lego2STL.Core.Pipeline;
using Lego2STL.Core.Rebrickable;
using Lego2STL.Core.Run;
using Lego2STL.Tests.Run;

namespace Lego2STL.Tests.Pipeline;

/// <summary>
/// What the report says about the parts a run deliberately did not build.
/// </summary>
/// <remarks>
/// The report is the page someone prints from, so a part missing from the plates has to be
/// accounted for on it - otherwise the plates read as the whole set, which is the same mistake
/// the missing-parts line already exists to prevent.
/// </remarks>
public sealed class NotPrintedPartsTests
{
    private static RunOutcome AnOutcomeLeavingAHoseOut(RunLayout layout) =>
        APretendRun.Complete(layout) with
        {
            PartFacts = new Dictionary<string, PartFact>(StringComparer.OrdinalIgnoreCase)
            {
                ["5102c13"] = new("Tubes and Hoses", "Rubber"),
                ["22127"] = new("Electronics", "Plastic"),
            },
            NotPrinted = ["5102c13", "22127"],
        };

    [Fact]
    public async Task The_report_names_every_part_it_did_not_build()
    {
        var layout = RunLayout.At(APretendRun.TempFolder("notprinted"));

        await RunReport.WriteAsync(layout, AnOutcomeLeavingAHoseOut(layout), CancellationToken.None);
        var report = await File.ReadAllTextAsync(layout.ReportPath);

        report.Should().Contain("5102c13").And.Contain("22127");
    }

    /// <summary>The material is named, because "rubber" is what makes the answer obvious.</summary>
    [Fact]
    public async Task The_report_says_what_ruled_each_one_out()
    {
        var layout = RunLayout.At(APretendRun.TempFolder("notprinted-why"));

        await RunReport.WriteAsync(layout, AnOutcomeLeavingAHoseOut(layout), CancellationToken.None);
        var report = await File.ReadAllTextAsync(layout.ReportPath);

        report.Should().Contain("Rubber", "the material is the answer for the hose");
    }

    /// <summary>A run that left nothing out gains no section, and no blank heading either.</summary>
    [Fact]
    public async Task A_run_that_built_everything_says_nothing_about_it()
    {
        var layout = RunLayout.At(APretendRun.TempFolder("notprinted-none"));

        await RunReport.WriteAsync(layout, APretendRun.Complete(layout), CancellationToken.None);
        var report = await File.ReadAllTextAsync(layout.ReportPath);

        report.Should().NotContain(Strings.For(DisplayLanguages.Fallback)[TextKey.ReportNotPrintedTitle]);
    }
}
```

Add whatever usings the file needs for `Strings`, `TextKey` and `DisplayLanguages`
(`Lego2STL.Core.Text`). If `APretendRun` is not visible from the `Pipeline` test folder, add
`using Lego2STL.Tests.Run;` — it is a normal internal helper in the same assembly.

- [ ] **Step 2: Run it and watch it fail**

```
dotnet test tests/Lego2STL.Tests/Lego2STL.Tests.csproj --filter "FullyQualifiedName~NotPrintedParts"
```

Expected: FAIL to compile — `RunOutcome` has neither `PartFacts` nor `NotPrinted`, and
`TextKey.ReportNotPrintedTitle` does not exist.

- [ ] **Step 3: Add the setting**

In `src/Lego2STL.Core/Pipeline/RunSettings.cs`, beside `NoSeamRepair` (around line 126):

```csharp
    /// <summary>
    /// Build every part, including the ones the parts database says cannot be printed.
    /// </summary>
    public bool PrintEverything { get; init; }
```

and in the same file's command-line rebuilder, beside the `NoSeamRepair` block (around line 382):

```csharp
        if (PrintEverything)
        {
            parts.Add("--print-everything");
        }
```

- [ ] **Step 4: Add the flag to the command line**

In `src/Lego2STL.Cli/Commands/PipelineOptions.cs`, beside `NoSeamRepair`:

```csharp
        PrintEverything = new Option<bool>("--print-everything")
        {
            Description = words[TextKey.HelpOptPrintEverything],
        };
```

```csharp
    public Option<bool> PrintEverything { get; }
```

Add `PrintEverything` to the option list around line 205, and to the settings built around
line 247:

```csharp
            PrintEverything = parseResult.GetValue(PrintEverything),
```

- [ ] **Step 5: Add the row to the window**

In `src/Lego2STL.Gui/ViewModels/RunOptionsViewModel.cs`, beside `NoSeamRepair`:

```csharp
    [ObservableProperty]
    public partial bool PrintEverything { get; set; }
```

and in the same file's settings mapping, beside `NoSeamRepair = NoSeamRepair,`:

```csharp
        PrintEverything = PrintEverything,
```

In `src/Lego2STL.Gui/ViewModels/OptionRowsViewModel.cs`, beside the `--no-seam-repair` row:

```csharp
            new ToggleOptionRow("--print-everything", TextKey.LabelOptPrintEverything,
                TextKey.HelpOptPrintEverything,
                () => o.PrintEverything, v => o.PrintEverything = v, fresh.PrintEverything),
```

- [ ] **Step 6: Add the wording**

`TextKey.cs`, beside `LabelOptNoSeamRepair` and `HelpOptNoSeamRepair` respectively:

```csharp
    LabelOptPrintEverything,
```

```csharp
    HelpOptPrintEverything,
```

and, beside `MsgWroteShapes`:

```csharp
    /// <summary>Said of a part left unbuilt because of what it is made of.</summary>
    MsgNotPrintedMaterial,

    /// <summary>Said of a part left unbuilt because of the kind of thing it is.</summary>
    MsgNotPrintedKind,

    /// <summary>Heading of the report's list of parts that were not built.</summary>
    ReportNotPrintedTitle,
```

`Strings.English.cs`:

```csharp
            [TextKey.LabelOptPrintEverything] = "Print everything",
            [TextKey.HelpOptPrintEverything] =
                "Build every part, including the ones that cannot be printed, such as electronics.",
            [TextKey.MsgNotPrintedMaterial] = "{0} is {1}, so it was not built; buy it instead.",
            [TextKey.MsgNotPrintedKind] = "{0} is a part to buy rather than print, so it was not built.",
            [TextKey.ReportNotPrintedTitle] = "Not printed:",
```

`Strings.Italian.cs`:

```csharp
            [TextKey.LabelOptPrintEverything] = "Stampa tutto",
            [TextKey.HelpOptPrintEverything] =
                "Costruisce ogni pezzo, compresi quelli non stampabili come l'elettronica.",
            [TextKey.MsgNotPrintedMaterial] = "{0} è in {1}, quindi non è stato costruito; va comprato.",
            [TextKey.MsgNotPrintedKind] = "{0} è un pezzo da comprare, non da stampare, quindi non è stato costruito.",
            [TextKey.ReportNotPrintedTitle] = "Non stampati:",
```

- [ ] **Step 7: Have the run ask before it builds**

In `src/Lego2STL.Core/Pipeline/PipelineRunner.cs`, in `BuildShapesAsync`, replace the single
line `var parts = list.DistinctPartNumbers;` (around line 470) with:

```csharp
        // Asked once for the whole run: the answer is the same for every copy of a part. Read
        // even when everything is to be printed, so the record still says what each part is.
        var facts = RebrickableDump.TryReadPartFacts(
            settings.ElementMap,
            Path.GetDirectoryName(Path.GetFullPath(settings.InputPath ?? ".")),
            Directory.GetCurrentDirectory());

        var (parts, notPrinted) = Printability.Choose(
            list.DistinctPartNumbers, facts, settings.PrintEverything);

        foreach (var partNumber in notPrinted)
        {
            var fact = facts.GetValueOrDefault(partNumber);

            _log("  " + (Printability.Of(fact) is Printable.NotItsMaterial
                ? words.Format(TextKey.MsgNotPrintedMaterial, partNumber, fact!.Material)
                : words.Format(TextKey.MsgNotPrintedKind, partNumber)));
        }
```

Add `using Lego2STL.Core.Catalogue;` and `using Lego2STL.Core.Rebrickable;` to the file if they
are not already there.

In the same method, add the two new members to the `new RunOutcome { ... }` it returns, after
`Failed = failed,`:

```csharp
            PartFacts = facts,
            NotPrinted = notPrinted,
```

- [ ] **Step 8: Carry them on the outcome**

In `src/Lego2STL.Core/Pipeline/RunOutcome.cs`, beside `Failed`:

```csharp
    /// <summary>What the parts database says about each part, when there is a database.</summary>
    public IReadOnlyDictionary<string, PartFact> PartFacts { get; init; } =
        new Dictionary<string, PartFact>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Parts deliberately not built, because they cannot be printed.</summary>
    public IReadOnlyList<string> NotPrinted { get; init; } = [];
```

Add `using Lego2STL.Core.Rebrickable;` if it is not already there.

A part left out this way is **not** a failure: it must not go into `Failed`, because `Failed`
decides whether the run comes out `Unverified`, and leaving a battery unbuilt is the run working.

- [ ] **Step 9: Say it in the report**

In `src/Lego2STL.Core/Pipeline/RunReport.cs`, insert a call between `Shapes(...)` and
`Plates(...)` (lines 43 and 45):

```csharp
        NotPrinted(sb, words, outcome);
```

and add the method beside `Plates`:

```csharp
    /// <summary>The parts left unbuilt on purpose, each with what ruled it out.</summary>
    private static void NotPrinted(StringBuilder sb, Strings words, RunOutcome outcome)
    {
        if (outcome.NotPrinted.Count == 0)
        {
            return;
        }

        sb.AppendLine(words[TextKey.ReportNotPrintedTitle]);

        foreach (var part in outcome.NotPrinted)
        {
            var fact = outcome.PartFacts.GetValueOrDefault(part);

            sb.AppendLine("  " + (Printability.Of(fact) is Printable.NotItsMaterial
                ? words.Format(TextKey.MsgNotPrintedMaterial, part, fact!.Material)
                : words.Format(TextKey.MsgNotPrintedKind, part)));
        }

        sb.AppendLine();
    }
```

Add `using Lego2STL.Core.Catalogue;` to the file if it is not already there.

- [ ] **Step 10: Run the whole suite**

```
dotnet test Lego2STL.slnx
```

Expected: PASS. `OptionParityTests` and `OptionRoundTripTests` prove the flag reaches the window
and survives a round trip; `StringsTests` proves both languages carry the five new keys.

- [ ] **Step 11: Commit**

```bash
git add -A
git commit -m "feat: a run no longer builds parts that cannot be printed"
```

---

### Task 4: The record keeps the verdict for every part

**Files:**
- Modify: `src/Lego2STL.Core/Run/RunManifest.cs`
- Modify: `src/Lego2STL.Core/Run/RunDocument.cs`
- Test: `tests/Lego2STL.Tests/Run/RunManifestTests.cs`

**Interfaces:**
- Consumes: `RunOutcome.PartFacts`, `RunOutcome.NotPrinted`, `Printability.Of`, `Printable.Token`.
- Produces: `ManifestPart.Printability` (`string?`, trailing optional) and
  `RunDocumentPart.Printability` (`string?`), plus, on `RunDocumentPart`, the three questions the
  card asks: `NotPrinted` (`bool`), `NotPrintedForMaterial` (`bool`), `IsUnknownPart` (`bool`).

- [ ] **Step 1: Write the failing test**

Add to `tests/Lego2STL.Tests/Run/RunManifestTests.cs`:

```csharp
    /// <summary>
    /// Why a part was not built is kept on the record, so a run reopened later says the same
    /// thing without the parts database being present.
    /// </summary>
    [Fact]
    public void A_part_that_was_not_built_says_why_on_the_record()
    {
        var layout = ARunFolder();

        var outcome = APretendRun.Complete(layout) with
        {
            PartFacts = new Dictionary<string, PartFact>(StringComparer.OrdinalIgnoreCase)
            {
                ["32523"] = new("Tubes and Hoses", "Rubber"),
            },
            NotPrinted = ["32523"],
        };

        var manifest = RunManifest.From(outcome, APretendRun.Started, APretendRun.Finished, null);
        var document = RunDocument.From(manifest, layout);

        var hose = document.Parts.Single(p => p.PartNumber == "32523");

        hose.Printability.Should().Be("material");
        hose.NotPrinted.Should().BeTrue();
        hose.NotPrintedForMaterial.Should().BeTrue();

        document.Parts.Where(p => p.PartNumber != "32523")
            .Should().OnlyContain(p => p.NotPrinted == false);
    }
```

Add `using Lego2STL.Core.Rebrickable;` to the file's usings.

- [ ] **Step 2: Run it and watch it fail**

```
dotnet test tests/Lego2STL.Tests/Lego2STL.Tests.csproj --filter "FullyQualifiedName~says_why_on_the_record"
```

Expected: FAIL to compile — `Printability` is not a member of either record.

- [ ] **Step 3: Add the field to the manifest**

In `src/Lego2STL.Core/Run/RunManifest.cs`, append to `ManifestPart`:

```csharp
    string? ElementId = null,
    string? Printability = null);
```

The `Part` factory takes the verdict from the caller rather than working it out, because the
caller holds the facts:

```csharp
    private static ManifestPart Part(
        PartEntry entry,
        Dictionary<string, PreparedMesh> shapes,
        IReadOnlyDictionary<string, PartFact> facts)
    {
        shapes.TryGetValue(entry.PartNumber, out var shape);
```

and its last argument becomes:

```csharp
            entry.ElementId,
            Core.Catalogue.Printability.Of(facts.GetValueOrDefault(entry.PartNumber)).Token());
```

In `RunManifest.From`, the projection changes to pass the facts:

```csharp
            Parts = [.. entries.Select(entry => Part(entry, shapes, outcome.PartFacts))],
```

Add `using Lego2STL.Core.Rebrickable;` if it is not already there.

- [ ] **Step 4: Add it to the document, with the questions the card asks**

In `src/Lego2STL.Core/Run/RunDocument.cs`, append to `RunDocumentPart`:

```csharp
    string? ElementId = null,
    string? Printability = null)
```

carry it in the projection, after `part.ElementId`:

```csharp
                    part.Printability)),
```

and add to the record's body, beside `HasSelfIntersection`:

```csharp
    /// <summary>True when the run deliberately did not build this part.</summary>
    public bool NotPrinted =>
        !Core.Catalogue.Printability.FromToken(Printability).IsPrinted();

    /// <summary>True when what it is made of is what ruled it out, rather than what it is.</summary>
    public bool NotPrintedForMaterial =>
        Core.Catalogue.Printability.FromToken(Printability) is Printable.NotItsMaterial;

    /// <summary>
    /// True when the parts database has never heard of this code.
    /// </summary>
    /// <remarks>
    /// Also true of every part of a run recorded before the database was consulted, which is why
    /// nothing is refused on this alone - it only chooses which sentence a card that has nothing
    /// else to show puts on itself.
    /// </remarks>
    public bool IsUnknownPart =>
        Core.Catalogue.Printability.FromToken(Printability) is Printable.Unknown;
```

Add `using Lego2STL.Core.Catalogue;` to the file if it is not already there.

- [ ] **Step 5: Run the whole suite**

```
dotnet test Lego2STL.slnx
```

Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "feat: a run records why a part was not built"
```

---

### Task 5: Measure the band the drawing occupies

**Files:**
- Create, then delete: a throwaway console probe under the scratchpad, never committed
- Modify: this plan file, Task 6's Step 3, with the measured rule

**Interfaces:** produces a measurement, not code.

This is the spec's one open question and it is answered by looking, not by choosing. Task 6
cannot be written honestly until it is done.

- [ ] **Step 1: Render the reference pages and locate the labels**

Write a throwaway program that opens `6324712.pdf`, takes `GetPage(370).Bitmap`, runs the same
`LabelLocator` the run uses, and prints each label's `Bounds`.

- [ ] **Step 2: Measure the drawing above each label**

For each label, walk upward from its top edge, one row of pixels at a time, within the label's
own left and right edges widened by half the label's width on each side. Count non-white pixels
per row. Print, per label: the first row above the label that is entirely white, the last row
above it that carries ink, and the height of the gap between them.

- [ ] **Step 3: Decide the rule from what the numbers say**

If the ink above each label ends at a clear gap — a run of white rows wider than a couple of
pixels — the rule is "up to the first gap of N white rows", and N is what the numbers show.
If the gaps are not clean, the rule is the fallback: the label's own width, extended upward by
three times the label's height, clipped to the page.

- [ ] **Step 4: Write the finding into this plan**

Replace the marked paragraph in Task 6, Step 3 with the rule the measurement chose and the
numbers behind it. Then delete the probe.

- [ ] **Step 5: Commit the plan**

```bash
git add docs/superpowers/plans/2026-08-30-not-printable-parts.md
git commit -m "docs: record how tall a part's drawing is on the reference pages"
```

---

### Task 6: The run keeps a picture of every part it read

**Files:**
- Modify: `src/Lego2STL.Core/Run/RunLayout.cs`
- Create: `src/Lego2STL.Core/Extraction/PartPicture.cs`
- Modify: `src/Lego2STL.Core/Pipeline/PipelineRunner.cs` (both reading stages)
- Test: `tests/Lego2STL.Tests/Extraction/PartPictureTests.cs` (create)

**Interfaces:**
- Consumes: `RowCrop.Extract`, `RowCrop.ToPng`, `PixelBounds`, the measurement from Task 5.
- Produces: `RunLayout.ImageDirectory` (`Root/images`); `PartPicture.BandAbove(PixelBounds label,
  int pageWidth, int pageHeight) → PixelBounds` and
  `PartPicture.TryWrite(SKBitmap page, PixelBounds label, string directory, string partNumber) → bool`,
  which writes `<partNumber>.png` and returns false when one is already there.

- [ ] **Step 1: Write the failing tests**

Create `tests/Lego2STL.Tests/Extraction/PartPictureTests.cs`:

```csharp
using FluentAssertions;
using Lego2STL.Core.Extraction;
using SkiaSharp;

namespace Lego2STL.Tests.Extraction;

/// <summary>
/// Cutting a part's drawing out of the page it was read from.
/// </summary>
/// <remarks>
/// The run knows where a label's text is and not where the drawing above it ends, so the band is
/// taken by rule rather than by detection. What the tests defend is that the band stays on the
/// page, sits above the label rather than over it, and that a part read twice keeps the first
/// picture instead of overwriting it.
/// </remarks>
public sealed class PartPictureTests : IDisposable
{
    private readonly string _folder = Path.Combine(
        Path.GetTempPath(), "lego2stl-pictures-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    /// <summary>A page with a black square where a drawing would be, and a label under it.</summary>
    private static SKBitmap APage()
    {
        var page = new SKBitmap(400, 400);
        using var canvas = new SKCanvas(page);
        canvas.Clear(SKColors.White);
        canvas.DrawRect(SKRect.Create(150, 100, 100, 80), new SKPaint { Color = SKColors.Black });
        return page;
    }

    private static PixelBounds ALabel() => new(160, 200, 240, 230);

    [Fact]
    public void The_band_sits_above_the_label_and_never_over_it()
    {
        var band = PartPicture.BandAbove(ALabel(), 400, 400);

        band.Bottom.Should().BeLessThan(ALabel().Top);
        band.Top.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public void The_band_is_wider_than_the_label_because_a_drawing_is()
    {
        var band = PartPicture.BandAbove(ALabel(), 400, 400);

        band.Width.Should().BeGreaterThan(ALabel().Width);
    }

    [Fact]
    public void A_label_near_an_edge_still_gives_a_band_inside_the_page()
    {
        var band = PartPicture.BandAbove(new PixelBounds(0, 4, 40, 20), 400, 400);

        band.Left.Should().BeGreaterThanOrEqualTo(0);
        band.Top.Should().BeGreaterThanOrEqualTo(0);
        band.Right.Should().BeLessThan(400);
    }

    [Fact]
    public void The_picture_is_written_where_the_catalogue_looks_for_it()
    {
        using var page = APage();

        PartPicture.TryWrite(page, ALabel(), _folder, "32523").Should().BeTrue();

        File.Exists(Path.Combine(_folder, "32523.png")).Should().BeTrue();
    }

    /// <summary>The same part in a second colour keeps the first picture, as the shapes do.</summary>
    [Fact]
    public void A_part_read_twice_keeps_the_first_picture()
    {
        using var page = APage();

        PartPicture.TryWrite(page, ALabel(), _folder, "32523").Should().BeTrue();
        var first = File.ReadAllBytes(Path.Combine(_folder, "32523.png"));

        PartPicture.TryWrite(page, new PixelBounds(0, 4, 40, 20), _folder, "32523")
            .Should().BeFalse("the first one read is the one kept");

        File.ReadAllBytes(Path.Combine(_folder, "32523.png")).Should().Equal(first);
    }

    /// <summary>A part number is also a file name, and some of them are not.</summary>
    [Fact]
    public void A_part_number_that_is_not_a_file_name_does_not_stop_the_run()
    {
        using var page = APage();

        var write = () => PartPicture.TryWrite(page, ALabel(), _folder, "3/4:5");

        write.Should().NotThrow();
    }
}
```

- [ ] **Step 2: Run them and watch them fail**

```
dotnet test tests/Lego2STL.Tests/Lego2STL.Tests.csproj --filter "FullyQualifiedName~PartPicture"
```

Expected: FAIL to compile — `PartPicture` does not exist.

- [ ] **Step 3: Write it**

Create `src/Lego2STL.Core/Extraction/PartPicture.cs`. **The band's rule and the numbers in it
come from Task 5; the values below are the fallback, to be replaced by what was measured:**

```csharp
using Lego2STL.Core.Ocr;
using SkiaSharp;

namespace Lego2STL.Core.Extraction;

/// <summary>
/// Cuts a part's drawing out of the page its label was read from.
/// </summary>
/// <remarks>
/// A catalogue prints the drawing above the label, and the run only ever located the label, so
/// the band is taken by rule. It is generous on purpose: a band too tall shows some white, while
/// a band too short cuts the part in half.
/// </remarks>
public static class PartPicture
{
    /// <summary>How many label heights of page to take above the label.</summary>
    public const int BandInLabelHeights = 3;

    /// <summary>How much of the label's width to add on each side, as a fraction.</summary>
    public const double SpreadEachSide = 0.5;

    /// <summary>The region a part's drawing occupies, above its label.</summary>
    public static PixelBounds BandAbove(PixelBounds label, int pageWidth, int pageHeight)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pageWidth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pageHeight);

        var spread = (int)(label.Width * SpreadEachSide);

        var left = Math.Max(0, label.Left - spread);
        var right = Math.Min(pageWidth - 1, label.Right + spread);
        var bottom = Math.Max(0, label.Top - 1);
        var top = Math.Max(0, label.Top - (BandInLabelHeights * Math.Max(1, label.Height)));

        return new PixelBounds(left, top, right, Math.Max(top, bottom));
    }

    /// <summary>
    /// Writes the drawing above a label as <c>&lt;part&gt;.png</c>, and says whether it did.
    /// </summary>
    /// <returns>
    /// False when a picture of that part is already there, or when it could not be written -
    /// neither of which is a reason to stop reading a document.
    /// </returns>
    public static bool TryWrite(SKBitmap page, PixelBounds label, string directory, string partNumber)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        ArgumentException.ThrowIfNullOrWhiteSpace(partNumber);

        var name = string.Concat(partNumber.Split(Path.GetInvalidFileNameChars()));
        if (name.Length == 0)
        {
            return false;
        }

        var path = Path.Combine(directory, name + ".png");

        try
        {
            if (File.Exists(path))
            {
                return false;
            }

            using var crop = RowCrop.Extract(page, BandAbove(label, page.Width, page.Height), padding: 0, margin: 0);

            Directory.CreateDirectory(directory);
            File.WriteAllBytes(path, RowCrop.ToPng(crop));
            return true;
        }
        catch (Exception ex) when (ex is IOException
                                       or UnauthorizedAccessException
                                       or InvalidOperationException)
        {
            return false;
        }
    }
}
```

- [ ] **Step 4: Give the run somewhere to put them**

In `src/Lego2STL.Core/Run/RunLayout.cs`, beside `StlDirectory`:

```csharp
    /// <summary>A picture of each part, cut from the document it was read from.</summary>
    public string ImageDirectory => Path.Combine(Root, "images");
```

- [ ] **Step 5: Write one while each page is read**

In `src/Lego2STL.Core/Pipeline/PipelineRunner.cs`, in `ReadPrintedPagesAsync`, the loop that
collects `printed` gains the page's own picture. Replace the body of the `foreach (var pageNumber
in pages)` loop with:

```csharp
            cancellationToken.ThrowIfCancellationRequested();

            var onPage = document.ReadPrintedCatalogue(pageNumber);
            notes.Add(words.Format(TextKey.NoteEntriesFound, pageNumber, onPage.Count));
            printed.AddRange(onPage.Select(e => (pageNumber, e)));

            if (onPage.Count > 0)
            {
                using var image = document.GetPage(pageNumber);

                foreach (var entry in onPage)
                {
                    PartPicture.TryWrite(image.Bitmap, entry.Bounds, layout.ImageDirectory, entry.ElementId);
                }
            }
```

The picture is filed under the element number here because that is all this stage knows; the
part number is only settled after the lookup. Rename it once the entry resolves, in the loop
that follows, right after `resolved` is obtained and found not to be null:

```csharp
            PartPicture.Rename(layout.ImageDirectory, entry.ElementId, resolved.PartNumber);
```

with, in `PartPicture`:

```csharp
    /// <summary>
    /// Files a picture under the part number once the element it was read as has been resolved.
    /// </summary>
    /// <remarks>
    /// The catalogue looks for a part number, and a page only ever knew an element number.
    /// </remarks>
    public static void Rename(string directory, string from, string to)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);

        try
        {
            var source = Path.Combine(directory, Safe(from) + ".png");
            var target = Path.Combine(directory, Safe(to) + ".png");

            if (File.Exists(source) && !File.Exists(target))
            {
                File.Move(source, target);
            }
            else if (File.Exists(source))
            {
                File.Delete(source);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A picture is a convenience; losing one is never worth stopping a run for.
        }
    }

    private static string Safe(string name) =>
        string.Concat(name.Split(Path.GetInvalidFileNameChars()));
```

Use `Safe` in `TryWrite` too, replacing the inline `string.Concat(...)`.

`ReadPrintedPagesAsync` needs the layout: add `RunLayout layout` as a parameter after `document`
and pass it from the one call site.

- [ ] **Step 6: Do the same on the path that reads pixels**

In `ReadPagesAsync`, the OCR path, each `PartLabel` already carries `Bounds` and the page bitmap
is in hand. After a label is read into a `CatalogueReading`, write its picture the same way,
filed under the part number that was read — no rename is needed there, because that path reads
the part number directly:

```csharp
                PartPicture.TryWrite(page.Bitmap, label.Bounds, layout.ImageDirectory, reading.PartNumber);
```

- [ ] **Step 7: Run the whole suite**

```
dotnet test Lego2STL.slnx
```

Expected: PASS.

- [ ] **Step 8: Look at what it produced**

```bash
dotnet run --project src/Lego2STL.Cli -- build 6324712.pdf --pages 370-371 --lang it --stages list
```

Open two or three PNGs from the run's `images/` folder. Each must show the part and not the text
under it. If they do not, the rule from Task 5 was wrong; fix it here rather than accepting the
pictures.

- [ ] **Step 9: Commit**

```bash
git add -A
git commit -m "feat: a run keeps a picture of each part from the document it read"
```

---

### Task 7: A photo for the parts no document showed

**Files:**
- Modify: `src/Lego2STL.Gui/Services/ThumbnailCache.cs`
- Test: `tests/Lego2STL.UiTests/ThumbnailCacheTests.cs` (create)

**Interfaces:**
- Consumes: nothing from earlier tasks.
- Produces: `ThumbnailCache.TryGetPhotoAsync(string partNumber, CancellationToken)` returning
  `Task<Bitmap?>` — the part's own photograph rather than a render in a colour, cached under
  `photo-<part>.png`, and null when offline or absent.

- [ ] **Step 1: Write the failing test**

Create `tests/Lego2STL.UiTests/ThumbnailCacheTests.cs`:

```csharp
using FluentAssertions;
using Lego2STL.Gui.Services;

namespace Lego2STL.UiTests;

/// <summary>
/// The pictures the catalogue shows, and what it does when it cannot have one.
/// </summary>
/// <remarks>
/// Offline is the case that matters: a run made with no network must not hang the catalogue on
/// a request that will never answer, and must not report an error either - a missing picture is
/// a missing picture.
/// </remarks>
public sealed class ThumbnailCacheTests : IDisposable
{
    private readonly string _folder = Path.Combine(
        Path.GetTempPath(), "lego2stl-thumbs-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    [Fact]
    public async Task Offline_there_is_no_photo_and_no_complaint()
    {
        using var cache = new ThumbnailCache(_folder) { Offline = true };

        (await cache.TryGetPhotoAsync("32523")).Should().BeNull();
    }

    [Fact]
    public async Task A_part_number_that_is_no_part_number_gives_nothing()
    {
        using var cache = new ThumbnailCache(_folder) { Offline = true };

        (await cache.TryGetPhotoAsync("  ")).Should().BeNull();
    }

    /// <summary>A photo already on the disk is used without going near the network.</summary>
    [Fact]
    public async Task A_photo_already_fetched_is_read_from_the_disk()
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllBytes(Path.Combine(_folder, "photo-32523.png"), OnePixelPng());

        using var cache = new ThumbnailCache(_folder) { Offline = true };

        (await cache.TryGetPhotoAsync("32523")).Should().NotBeNull();
    }

    /// <summary>The smallest valid PNG: one opaque pixel.</summary>
    private static byte[] OnePixelPng() => Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");
}
```

- [ ] **Step 2: Run it and watch it fail**

```
dotnet test tests/Lego2STL.UiTests/Lego2STL.UiTests.csproj --filter "FullyQualifiedName~ThumbnailCache"
```

Expected: FAIL to compile — `TryGetPhotoAsync` does not exist.

- [ ] **Step 3: Write it**

In `src/Lego2STL.Gui/Services/ThumbnailCache.cs`, beside `UrlPattern`:

```csharp
    private const string PhotoUrlPattern = "https://cdn.rebrickable.com/media/parts/photos/{0}.jpg";
```

and beside `TryGetAsync`:

```csharp
    /// <summary>
    /// A photograph of the part itself, for the ones no render exists for.
    /// </summary>
    /// <remarks>
    /// The renders the catalogue normally shows are drawn from the shape library, so the parts
    /// this is for - electronics, hoses, anything with no shape file - have no render at all.
    /// A photograph has no colour of its own to choose, which is why this one is not asked for
    /// one.
    /// </remarks>
    public async Task<Bitmap?> TryGetPhotoAsync(
        string partNumber,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(partNumber))
        {
            return null;
        }

        var path = Path.Combine(_directory, $"photo-{Safe(partNumber)}.png");

        try
        {
            if (File.Exists(path))
            {
                return Load(path);
            }

            if (Offline)
            {
                return null;
            }

            await _atOnce.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var url = string.Format(
                    System.Globalization.CultureInfo.InvariantCulture, PhotoUrlPattern, partNumber);

                using var response = await _http.GetAsync(url, cancellationToken).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }

                Directory.CreateDirectory(_directory);
                var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
                await File.WriteAllBytesAsync(path, bytes, cancellationToken).ConfigureAwait(false);

                return Load(path);
            }
            finally
            {
                _atOnce.Release();
            }
        }
        catch (Exception ex) when (ex is HttpRequestException
                                       or IOException
                                       or TaskCanceledException
                                       or UnauthorizedAccessException
                                       or ArgumentException)
        {
            return null;
        }
    }
```

- [ ] **Step 4: Run the tests, then the suite**

```
dotnet test Lego2STL.slnx
```

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat: the catalogue can show a photograph of a part with no render"
```

---

### Task 8: The shops, and the address of a part in one

**Files:**
- Create: `src/Lego2STL.Gui/Services/Shop.cs`
- Modify: `src/Lego2STL.Gui/Services/UserSettings.cs`
- Test: `tests/Lego2STL.UiTests/ShopTests.cs` (create)

**Interfaces:**
- Consumes: nothing from earlier tasks.
- Produces: `sealed record Shop(string Name, string Url, string? Search)` and
  `static class Shops` with `Defaults` (`IReadOnlyList<Shop>`) and
  `AddressOf(Shop shop, string partNumber, string? elementId, int colorCode) → string?`;
  `UserSettings.Shops` (`List<Shop>`) and `UserSettings.PreferredShop` (`string?`).

- [ ] **Step 1: Write the failing tests**

Create `tests/Lego2STL.UiTests/ShopTests.cs`:

```csharp
using FluentAssertions;
using Lego2STL.Gui.Services;

namespace Lego2STL.UiTests;

/// <summary>
/// Turning a part into an address at a shop.
/// </summary>
/// <remarks>
/// The rule that matters is the one about element numbers: a shop that sells by element number
/// cannot be given a part read from a CSV, which has none, and answering with an address that
/// leads nowhere is worse than answering with a search.
/// </remarks>
public sealed class ShopTests
{
    private static readonly Shop ByPart = new("A shop", "https://shop/part/{part}", "https://shop/find?q={part}");
    private static readonly Shop ByElement = new("Another", "https://other/{element}", "https://other/find?q={part}");

    [Fact]
    public void A_part_number_goes_where_the_template_says()
    {
        Shops.AddressOf(ByPart, "32523", elementId: null, colorCode: 11)
            .Should().Be("https://shop/part/32523");
    }

    [Fact]
    public void An_element_number_is_used_when_the_shop_asks_for_one()
    {
        Shops.AddressOf(ByElement, "32523", "6177114", 11).Should().Be("https://other/6177114");
    }

    /// <summary>A list from a CSV has no element numbers, so the shop's search is used instead.</summary>
    [Fact]
    public void A_shop_that_needs_an_element_number_falls_back_to_its_search()
    {
        Shops.AddressOf(ByElement, "32523", elementId: null, colorCode: 11)
            .Should().Be("https://other/find?q=32523");
    }

    [Fact]
    public void A_shop_that_needs_one_and_has_no_search_has_no_address()
    {
        var awkward = new Shop("Awkward", "https://awkward/{element}", Search: null);

        Shops.AddressOf(awkward, "32523", elementId: null, colorCode: 11).Should().BeNull();
    }

    [Fact]
    public void A_colour_code_is_substituted_when_the_template_wants_one()
    {
        var byColour = new Shop("Colourful", "https://c/{part}?colour={color}", null);

        Shops.AddressOf(byColour, "32523", null, 11).Should().Be("https://c/32523?colour=11");
    }

    /// <summary>A part number goes into an address, so it has to be escaped like one.</summary>
    [Fact]
    public void A_part_number_with_awkward_characters_is_escaped()
    {
        Shops.AddressOf(ByPart, "3 4&5", null, 11).Should().Be("https://shop/part/3%204%265");
    }

    [Fact]
    public void The_three_shops_offered_at_first_all_produce_an_address()
    {
        Shops.Defaults.Should().HaveCount(3);

        foreach (var shop in Shops.Defaults)
        {
            Shops.AddressOf(shop, "32523", "6177114", 11).Should().NotBeNullOrWhiteSpace();
        }
    }

    [Fact]
    public void The_shops_survive_being_written_and_read_back()
    {
        var settings = new UserSettings
        {
            Shops = [new Shop("Mine", "https://mine/{part}", null)],
            PreferredShop = "Mine",
        };

        var json = System.Text.Json.JsonSerializer.Serialize(settings);
        var read = System.Text.Json.JsonSerializer.Deserialize<UserSettings>(json)!;

        read.Shops.Should().ContainSingle().Which.Url.Should().Be("https://mine/{part}");
        read.PreferredShop.Should().Be("Mine");
    }
}
```

- [ ] **Step 2: Run them and watch them fail**

```
dotnet test tests/Lego2STL.UiTests/Lego2STL.UiTests.csproj --filter "FullyQualifiedName~ShopTests"
```

Expected: FAIL to compile.

- [ ] **Step 3: Write it**

Create `src/Lego2STL.Gui/Services/Shop.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json.Serialization;

namespace Lego2STL.Gui.Services;

/// <summary>
/// Somewhere a part can be bought, and how to build the address of one there.
/// </summary>
/// <param name="Url">
/// The address of a part's own page, with <c>{part}</c>, <c>{element}</c> and <c>{color}</c>
/// standing for what is known about it.
/// </param>
/// <param name="Search">
/// Where to search, for a part this shop's own page cannot be built for. Optional.
/// </param>
public sealed record Shop(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("url")] string Url,
    [property: JsonPropertyName("search")] string? Search);

/// <summary>The shops offered, and the addresses they lead to.</summary>
public static class Shops
{
    /// <summary>What the settings start with, and what a cleared list goes back to.</summary>
    public static IReadOnlyList<Shop> Defaults { get; } =
    [
        new("BrickLink",
            "https://www.bricklink.com/v2/catalog/catalogitem.page?P={part}",
            "https://www.bricklink.com/v2/search.page?q={part}"),
        new("Rebrickable",
            "https://rebrickable.com/parts/{part}/",
            "https://rebrickable.com/search/?q={part}"),
        new("LEGO Pick a Brick",
            "https://www.lego.com/pick-and-build/pick-a-brick?query={element}",
            "https://www.lego.com/pick-and-build/pick-a-brick?query={part}"),
    ];

    /// <summary>
    /// Where this shop sells this part, or null when it cannot be said.
    /// </summary>
    /// <remarks>
    /// A shop that sells by element number is no use to a list that has none - a list read from
    /// a CSV or from a set number - so its search is used instead, and when it has no search
    /// there is no honest address to give.
    /// </remarks>
    public static string? AddressOf(Shop shop, string partNumber, string? elementId, int colorCode)
    {
        ArgumentNullException.ThrowIfNull(shop);

        var wantsElement = shop.Url.Contains("{element}", StringComparison.Ordinal);
        var template = wantsElement && string.IsNullOrWhiteSpace(elementId) ? shop.Search : shop.Url;

        if (string.IsNullOrWhiteSpace(template))
        {
            return null;
        }

        if (template.Contains("{element}", StringComparison.Ordinal) &&
            string.IsNullOrWhiteSpace(elementId))
        {
            return null;
        }

        return template
            .Replace("{part}", Uri.EscapeDataString(partNumber ?? string.Empty), StringComparison.Ordinal)
            .Replace("{element}", Uri.EscapeDataString(elementId ?? string.Empty), StringComparison.Ordinal)
            .Replace("{color}", colorCode.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);
    }
}
```

In `src/Lego2STL.Gui/Services/UserSettings.cs`, beside `PartNumbering`:

```csharp
    /// <summary>Where parts can be bought, in the order they are offered.</summary>
    [JsonPropertyName("shops")]
    public List<Shop> Shops { get; set; } = [];

    /// <summary>The name of the shop whose button the catalogue shows.</summary>
    [JsonPropertyName("preferredShop")]
    public string? PreferredShop { get; set; }
```

- [ ] **Step 4: Run the tests, then the suite**

```
dotnet test Lego2STL.slnx
```

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat: the shops a part can be bought from, and their addresses"
```

---

### Task 9: The settings hold the list, and let it be changed

**Files:**
- Modify: `src/Lego2STL.Gui/ViewModels/SettingsViewModel.cs`
- Modify: `src/Lego2STL.Gui/Views/SettingsView.axaml`
- Modify: `src/Lego2STL.Core/Text/TextKey.cs`, `Strings.English.cs`, `Strings.Italian.cs`
- Test: `tests/Lego2STL.UiTests/SettingsTests.cs`

**Interfaces:**
- Consumes: `Shop`, `Shops.Defaults`, `UserSettings.Shops`, `UserSettings.PreferredShop`.
- Produces: on `SettingsViewModel`, `ShopRows` (`ObservableCollection<ShopRowViewModel>`),
  `AddShopCommand`, `RemoveShopCommand`; `ShopRowViewModel` with observable `Name`, `Url`,
  `Search` and `IsPreferred`, and a `ToShop()`.

**How this screen already works, and must keep working.** `SettingsViewModel` is constructed as
`new SettingsViewModel(RunOptionsViewModel options, UserSettings saved, RunsViewModel? runs)`,
with a parameterless constructor for the designer. **There is no `Apply`**: every setting writes
itself into `_saved` and calls `_saved.Save()` the moment it changes — see `SelectedLanguage`.
The shops follow that pattern. The UI test project already redirects the preferences file to a
temporary folder from a module initialiser (`Isolation.cs`), so a test may save freely.

- [ ] **Step 1: Write the failing tests**

Add to `tests/Lego2STL.UiTests/SettingsTests.cs`:

```csharp
    private static SettingsViewModel ASettingsScreen(UserSettings saved) =>
        new(new RunOptionsViewModel(), saved, null);

    /// <summary>The list starts at the three shops rather than empty.</summary>
    [AvaloniaFact]
    public void The_shops_start_at_the_three_offered()
    {
        var settings = ASettingsScreen(new UserSettings());

        settings.ShopRows.Should().HaveCount(3);
        settings.ShopRows.Should().ContainSingle(row => row.IsPreferred);
    }

    /// <summary>A list already chosen is the one shown, not the offered three.</summary>
    [AvaloniaFact]
    public void A_list_already_saved_is_the_one_shown()
    {
        var saved = new UserSettings
        {
            Shops = [new Shop("Mine", "https://mine/{part}", null)],
            PreferredShop = "Mine",
        };

        var settings = ASettingsScreen(saved);

        settings.ShopRows.Should().ContainSingle().Which.Name.Should().Be("Mine");
        settings.ShopRows[0].IsPreferred.Should().BeTrue();
    }

    [AvaloniaFact]
    public void A_shop_can_be_added_and_taken_away_again()
    {
        var settings = ASettingsScreen(new UserSettings());
        var before = settings.ShopRows.Count;

        settings.AddShopCommand.Execute(null);
        settings.ShopRows.Should().HaveCount(before + 1);

        settings.RemoveShopCommand.Execute(settings.ShopRows[^1]);
        settings.ShopRows.Should().HaveCount(before);
    }

    /// <summary>Taking away the preferred shop leaves a preference that still means something.</summary>
    [AvaloniaFact]
    public void Taking_away_the_preferred_shop_promotes_another()
    {
        var settings = ASettingsScreen(new UserSettings());

        settings.RemoveShopCommand.Execute(settings.ShopRows.First(row => row.IsPreferred));

        settings.ShopRows.Should().ContainSingle(row => row.IsPreferred);
    }

    /// <summary>Only ever one preferred shop, however many are asked for.</summary>
    [AvaloniaFact]
    public void Choosing_one_shop_unchooses_the_others()
    {
        var settings = ASettingsScreen(new UserSettings());

        settings.ShopRows[2].IsPreferred = true;

        settings.ShopRows.Should().ContainSingle(row => row.IsPreferred);
        settings.ShopRows[2].IsPreferred.Should().BeTrue();
    }

    /// <summary>An edit is kept the moment it is made, as every other setting here is.</summary>
    [AvaloniaFact]
    public void An_edited_shop_is_written_down_at_once()
    {
        var saved = new UserSettings();
        var settings = ASettingsScreen(saved);

        settings.ShopRows[0].Name = "My shop";

        saved.Shops[0].Name.Should().Be("My shop");
        saved.PreferredShop.Should().Be(settings.ShopRows.First(row => row.IsPreferred).Name);
    }
```

- [ ] **Step 2: Run them and watch them fail**

```
dotnet test tests/Lego2STL.UiTests/Lego2STL.UiTests.csproj --filter "FullyQualifiedName~SettingsTests"
```

Expected: FAIL to compile.

- [ ] **Step 3: Add the row view model and the list**

In `src/Lego2STL.Gui/ViewModels/SettingsViewModel.cs`:

```csharp
/// <summary>One shop, as the settings let it be edited.</summary>
public sealed partial class ShopRowViewModel : ObservableObject
{
    public ShopRowViewModel(Shop shop, bool isPreferred)
    {
        ArgumentNullException.ThrowIfNull(shop);

        Name = shop.Name;
        Url = shop.Url;
        Search = shop.Search;
        IsPreferred = isPreferred;
    }

    [ObservableProperty]
    public partial string Name { get; set; }

    [ObservableProperty]
    public partial string Url { get; set; }

    [ObservableProperty]
    public partial string? Search { get; set; }

    /// <summary>Whether this is the shop the catalogue's button opens.</summary>
    [ObservableProperty]
    public partial bool IsPreferred { get; set; }

    public Shop ToShop() => new(Name, Url, Search);
}
```

and on `SettingsViewModel`:

```csharp
    /// <summary>Where parts can be bought. Starts at the three offered, and can be changed.</summary>
    public ObservableCollection<ShopRowViewModel> ShopRows { get; } = [];

    /// <summary>
    /// Builds the rows, and keeps the file in step with them from then on.
    /// </summary>
    /// <remarks>
    /// Saved as it is edited rather than on leaving the screen, because that is how every other
    /// setting here behaves and a screen with two habits is a screen that loses one of them.
    /// </remarks>
    private void FillShops()
    {
        var shops = _saved.Shops.Count > 0 ? _saved.Shops : Shops.Defaults;

        var preferred = shops.FirstOrDefault(
            s => string.Equals(s.Name, _saved.PreferredShop, StringComparison.Ordinal)) ?? shops[0];

        foreach (var shop in shops)
        {
            Add(new ShopRowViewModel(shop, shop == preferred));
        }

        RememberShops();
    }

    private void Add(ShopRowViewModel row)
    {
        row.PropertyChanged += (sender, e) =>
        {
            if (e.PropertyName == nameof(ShopRowViewModel.IsPreferred) && row.IsPreferred)
            {
                foreach (var other in ShopRows.Where(r => r != row))
                {
                    other.IsPreferred = false;
                }
            }

            RememberShops();
        };

        ShopRows.Add(row);
    }

    private void RememberShops()
    {
        _saved.Shops = [.. ShopRows.Select(r => r.ToShop())];
        _saved.PreferredShop = ShopRows.FirstOrDefault(r => r.IsPreferred)?.Name;
        _saved.Save();
    }

    [RelayCommand]
    private void AddShop() => Add(new ShopRowViewModel(
        new Shop(string.Empty, "https://", null), isPreferred: ShopRows.Count == 0));

    /// <summary>Taking away the preferred shop promotes whichever is left, so one is always chosen.</summary>
    [RelayCommand]
    private void RemoveShop(ShopRowViewModel? row)
    {
        if (row is null || !ShopRows.Remove(row))
        {
            return;
        }

        if (row.IsPreferred && ShopRows.Count > 0)
        {
            ShopRows[0].IsPreferred = true;
        }

        RememberShops();
    }
```

Call `FillShops()` as the last statement of the three-argument constructor. Add
`using System;`, `using System.Collections.ObjectModel;` and `using CommunityToolkit.Mvvm.ComponentModel;`
to the file if they are not already there.

- [ ] **Step 4: Add the wording**

`TextKey.cs`, beside `UiSettings`:

```csharp
    /// <summary>The settings card holding the list of shops.</summary>
    UiShops,

    /// <summary>The button that adds a row to it.</summary>
    UiAddShop,

    /// <summary>The button that takes one away.</summary>
    UiRemoveShop,

    /// <summary>Marks the shop whose button the catalogue shows.</summary>
    UiPreferredShop,

    /// <summary>Explains what a shop's address may contain.</summary>
    UiShopHelp,
```

`Strings.English.cs`:

```csharp
            [TextKey.UiShops] = "Where to buy a part",
            [TextKey.UiAddShop] = "Add a shop",
            [TextKey.UiRemoveShop] = "Remove",
            [TextKey.UiPreferredShop] = "Preferred",
            [TextKey.UiShopHelp] =
                "In an address, {part} is the part number, {element} the LEGO element number and {color} the colour code.",
```

`Strings.Italian.cs`:

```csharp
            [TextKey.UiShops] = "Dove comprare un pezzo",
            [TextKey.UiAddShop] = "Aggiungi un negozio",
            [TextKey.UiRemoveShop] = "Togli",
            [TextKey.UiPreferredShop] = "Preferito",
            [TextKey.UiShopHelp] =
                "In un indirizzo, {part} è il codice pezzo, {element} il numero elemento LEGO e {color} il codice colore.",
```

- [ ] **Step 5: Show it**

In `src/Lego2STL.Gui/Views/SettingsView.axaml`, add a card at the end of the outer `StackPanel`,
following the shape of the cards already there:

```xml
      <Border Classes="card">
        <StackPanel Spacing="8">
          <TextBlock Classes="label"
                     Text="{Binding [UiShops], Source={x:Static loc:Loc.Current}}" />
          <TextBlock Classes="flag" TextWrapping="Wrap"
                     Text="{Binding [UiShopHelp], Source={x:Static loc:Loc.Current}}" />

          <ItemsControl ItemsSource="{Binding ShopRows}">
            <ItemsControl.ItemTemplate>
              <DataTemplate x:DataType="vm:ShopRowViewModel">
                <Grid ColumnDefinitions="Auto,150,*,Auto" Margin="0,0,0,6">
                  <RadioButton Grid.Column="0" GroupName="PreferredShop" VerticalAlignment="Center"
                               Margin="0,0,8,0"
                               ToolTip.Tip="{Binding [UiPreferredShop], Source={x:Static loc:Loc.Current}}"
                               IsChecked="{Binding IsPreferred, Mode=TwoWay}" />
                  <TextBox Grid.Column="1" Margin="0,0,6,0" Text="{Binding Name}" />
                  <TextBox Grid.Column="2" Margin="0,0,6,0" Text="{Binding Url}" />
                  <Button Grid.Column="3"
                          Content="{Binding [UiRemoveShop], Source={x:Static loc:Loc.Current}}"
                          Command="{Binding $parent[ItemsControl].((vm:SettingsViewModel)DataContext).RemoveShopCommand}"
                          CommandParameter="{Binding}" />
                </Grid>
              </DataTemplate>
            </ItemsControl.ItemTemplate>
          </ItemsControl>

          <Button HorizontalAlignment="Left"
                  Content="{Binding [UiAddShop], Source={x:Static loc:Loc.Current}}"
                  Command="{Binding AddShopCommand}" />
        </StackPanel>
      </Border>
```

The radio buttons share a group name, so the control keeps one chosen on screen; the view model
keeps one chosen in the data, which is what the test in Step 1 defends. Both are needed: the
group alone would not survive a row being removed.

- [ ] **Step 6: Run the whole suite**

```
dotnet test Lego2STL.slnx
```

Expected: PASS.

- [ ] **Step 7: Commit**

```bash
git add -A
git commit -m "feat: the settings hold the shops a part can be bought from"
```

---

### Task 10: The card shows the picture and offers to buy

**Files:**
- Modify: `src/Lego2STL.Gui/ViewModels/CataloguePartViewModel.cs`
- Modify: `src/Lego2STL.Gui/ViewModels/RunCatalogue.cs`
- Modify: `src/Lego2STL.Gui/ViewModels/RunDocumentViewModel.cs`
- Modify: `src/Lego2STL.Gui/ViewModels/MainViewModel.cs`
- Modify: `src/Lego2STL.Gui/Views/CatalogueView.axaml`
- Modify: `src/Lego2STL.Core/Text/TextKey.cs`, `Strings.English.cs`, `Strings.Italian.cs`
- Test: `tests/Lego2STL.UiTests/CatalogueTests.cs`

**Interfaces:**
- Consumes: `RunDocumentPart.NotPrinted`, `.NotPrintedForMaterial`, `.IsUnknownPart` (Task 4);
  `RunLayout.ImageDirectory` (Task 6); `ThumbnailCache.TryGetPhotoAsync` (Task 7);
  `Shop`, `Shops.AddressOf` (Task 8).
- Produces: on `CataloguePartViewModel`, `CanBuy` (`bool`), `BuyText` (`string`),
  `BuyCommand`, and a `Shop?` passed in by `RunCatalogue.Build`.

- [ ] **Step 1: Write the failing tests**

Add to `tests/Lego2STL.UiTests/CatalogueTests.cs`:

```csharp
    /// <summary>A part that was not printed says so and offers to be bought.</summary>
    [AvaloniaFact]
    public void A_part_that_cannot_be_printed_offers_to_be_bought()
    {
        var part = new RunDocumentPart(
            1, "5102c13", 11, "Black", Rgb24.Parse("#05131D"), 3,
            Title: null, Size: null,
            IsClosed: null, OpenEdgeCount: null, ThinnestSpanMm: null,
            OverusedEdgeCount: null, ClosedAtTolerance: null,
            ElementId: "6177114", Printability: "material");

        var card = new CataloguePartViewModel(
            part, null, null, doesNotFitThePlate: false, shop: Shops.Defaults[0]);

        card.HasWarning.Should().BeTrue();
        card.CanBuy.Should().BeTrue();
        card.WarningText.Should().Contain(Loc.Current.Text(TextKey.UiNotPrintedMaterial));
    }

    /// <summary>An ordinary part is not offered for sale; it was printed.</summary>
    [AvaloniaFact]
    public void A_part_that_was_printed_is_not_offered_for_sale()
    {
        var part = new RunDocumentPart(
            1, "32523", 11, "Black", Rgb24.Parse("#05131D"), 4,
            Title: "a beam", Size: "32 x 16 x 8 mm",
            IsClosed: true, OpenEdgeCount: 0, ThinnestSpanMm: 8,
            OverusedEdgeCount: 0, ClosedAtTolerance: null,
            ElementId: null, Printability: "yes");

        var card = new CataloguePartViewModel(
            part, null, null, doesNotFitThePlate: false, shop: Shops.Defaults[0]);

        card.CanBuy.Should().BeFalse();
        card.HasWarning.Should().BeFalse();
    }

    /// <summary>With no shop there is nothing to press, and the card still stands.</summary>
    [AvaloniaFact]
    public void With_no_shop_chosen_nothing_is_offered()
    {
        var part = new RunDocumentPart(
            1, "5102c13", 11, "Black", Rgb24.Parse("#05131D"), 3,
            Title: null, Size: null,
            IsClosed: null, OpenEdgeCount: null, ThinnestSpanMm: null,
            OverusedEdgeCount: null, ClosedAtTolerance: null,
            ElementId: null, Printability: "material");

        var card = new CataloguePartViewModel(part, null, null, false, shop: null);

        card.CanBuy.Should().BeFalse();
        card.HasWarning.Should().BeTrue("it still has to say why there is no shape");
    }
```

- [ ] **Step 2: Run them and watch them fail**

```
dotnet test tests/Lego2STL.UiTests/Lego2STL.UiTests.csproj --filter "FullyQualifiedName~offered"
```

Expected: FAIL to compile.

- [ ] **Step 3: Add the wording**

`TextKey.cs`, beside `UiDoesNotFitThePlate`:

```csharp
    /// <summary>Said of a part left unbuilt because of what it is made of.</summary>
    UiNotPrintedMaterial,

    /// <summary>Said of a part left unbuilt because of the kind of thing it is.</summary>
    UiNotPrintedKind,

    /// <summary>Said of a part whose shape the run could not build.</summary>
    UiNoShapeWasBuilt,

    /// <summary>Said of a code neither the parts database nor the shape library knows.</summary>
    UiPartNotRecognised,

    /// <summary>The button that opens a shop at this part.</summary>
    UiBuy,

    /// <summary>The same button when all it can do is search.</summary>
    UiSearchForIt,
```

`Strings.English.cs`:

```csharp
            [TextKey.UiNotPrintedMaterial] = "This part cannot be printed; it has to be bought.",
            [TextKey.UiNotPrintedKind] = "This is a part to buy rather than print.",
            [TextKey.UiNoShapeWasBuilt] = "No shape could be built for this part.",
            [TextKey.UiPartNotRecognised] = "This code was not recognised, so nothing was built for it.",
            [TextKey.UiBuy] = "Buy it",
            [TextKey.UiSearchForIt] = "Search for it",
```

`Strings.Italian.cs`:

```csharp
            [TextKey.UiNotPrintedMaterial] = "Questo pezzo non si può stampare; va comprato.",
            [TextKey.UiNotPrintedKind] = "Questo è un pezzo da comprare, non da stampare.",
            [TextKey.UiNoShapeWasBuilt] = "Non è stato possibile costruire la forma di questo pezzo.",
            [TextKey.UiPartNotRecognised] = "Questo codice non è stato riconosciuto, quindi non è stato costruito nulla.",
            [TextKey.UiBuy] = "Compralo",
            [TextKey.UiSearchForIt] = "Cercalo",
```

- [ ] **Step 4: Teach the card**

In `src/Lego2STL.Gui/ViewModels/CataloguePartViewModel.cs`, extend the constructor and add the
members. The constructor becomes:

```csharp
    public CataloguePartViewModel(
        RunDocumentPart part,
        string? shapePath,
        string? platePath,
        bool doesNotFitThePlate = false,
        bool noShapeWasBuilt = false,
        Shop? shop = null)
    {
        Part = part;
        ShapePath = shapePath;
        PlatePath = platePath;
        DoesNotFitThePlate = doesNotFitThePlate;
        NoShapeWasBuilt = noShapeWasBuilt;
        _shop = shop;

        Swatch = new SolidColorBrush(Color.FromRgb(part.Rgb.R, part.Rgb.G, part.Rgb.B));
    }

    private readonly Shop? _shop;
```

and beside `DoesNotFitThePlate`:

```csharp
    /// <summary>True when the run tried to build this part's shape and could not.</summary>
    public bool NoShapeWasBuilt { get; }

    /// <summary>Where this part can be bought, or null when nothing honest can be offered.</summary>
    public string? BuyAddress => _shop is null
        ? null
        : Shops.AddressOf(_shop, PartNumber, Part.ElementId, BrickLinkColorCode);

    public bool CanBuy => BuyAddress is not null && (Part.NotPrinted || NoShapeWasBuilt);

    /// <summary>Searching is all that can be offered for a code nothing recognised.</summary>
    public string BuyText => Localization.Loc.Current.Text(
        Part.IsUnknownPart ? Core.Text.TextKey.UiSearchForIt : Core.Text.TextKey.UiBuy);

    [RelayCommand]
    private void Buy()
    {
        if (BuyAddress is { } address)
        {
            Desktop.Open(address);
        }
    }
```

`HasWarning` gains the two new reasons:

```csharp
    public bool HasWarning =>
        Part.HasWarning || DoesNotFitThePlate || Part.NotPrinted || NoShapeWasBuilt;
```

and `Warnings` gains three entries, after the `DoesNotFitThePlate` block:

```csharp
            if (Part.NotPrinted)
            {
                warnings.Add(Localization.Loc.Current.Text(Part.NotPrintedForMaterial
                    ? Core.Text.TextKey.UiNotPrintedMaterial
                    : Core.Text.TextKey.UiNotPrintedKind));
            }

            if (NoShapeWasBuilt)
            {
                warnings.Add(Localization.Loc.Current.Text(Part.IsUnknownPart
                    ? Core.Text.TextKey.UiPartNotRecognised
                    : Core.Text.TextKey.UiNoShapeWasBuilt));
            }
```

- [ ] **Step 5: Fill the cards with what they need**

In `src/Lego2STL.Gui/ViewModels/RunCatalogue.cs`, `Build` gains the chosen shop and works out
which parts failed:

```csharp
    public static IReadOnlyList<CataloguePartViewModel> Build(RunDocument document, Shop? shop = null)
    {
        var plates = PlatesIn(document.PlateDirectory);

        var tooBig = document.DidNotFit
            .Select(part => part.PartNumber)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var noShape = document.Failed
            .Select(failure => failure.Part)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return
        [
            .. document.Parts.Select(part =>
            {
                var shape = Path.Combine(document.StlDirectory, part.PartNumber + ".stl");

                return new CataloguePartViewModel(
                    part,
                    File.Exists(shape) ? shape : null,
                    PlateFor(document, plates, part),
                    tooBig.Contains(part.PartNumber),
                    noShape.Contains(part.PartNumber),
                    shop);
            }),
        ];
    }
```

`LoadPicturesAsync` prefers, in order, the picture the run cut from the document, then the
render, then the photograph:

```csharp
        foreach (var part in parts)
        {
            var cut = Path.Combine(imageDirectory, part.PartNumber + ".png");

            if (File.Exists(cut))
            {
                part.Picture = Load(cut);
                continue;
            }

            if (ColorReference.Table.TryGet(ColorScheme.BrickLink, part.BrickLinkColorCode, out var colour))
            {
                part.Picture = await thumbnails.TryGetAsync(part.PartNumber, colour).ConfigureAwait(true);
            }

            part.Picture ??= await thumbnails.TryGetPhotoAsync(part.PartNumber).ConfigureAwait(true);
        }
```

with `imageDirectory` added as a parameter of `LoadPicturesAsync`, passed from
`RunDocumentViewModel.Fill()` as `Document.ImageDirectory`, and this beside `PlatesIn`:

```csharp
    /// <summary>A picture already on the disk, or nothing when it cannot be read.</summary>
    private static Bitmap? Load(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            return new Bitmap(stream);
        }
        catch (Exception ex) when (ex is IOException or ArgumentException)
        {
            return null;
        }
    }
```

with `using Avalonia.Media.Imaging;` added to the file.

In `src/Lego2STL.Core/Run/RunDocument.cs`, beside `StlDirectory`:

```csharp
    /// <summary>Where the pictures cut from the document live.</summary>
    public string ImageDirectory { get; init; } = string.Empty;
```

and in both projections in that file, beside where `StlDirectory` is set:

```csharp
            ImageDirectory = layout.ImageDirectory,
```

- [ ] **Step 6: Give the page the chosen shop**

In `src/Lego2STL.Gui/ViewModels/RunDocumentViewModel.cs`, beside `Numbering`:

```csharp
    /// <summary>Where the buy button leads. Set by the window, which owns the preference.</summary>
    public Shop? Shop { get; set; }
```

and pass it in `Fill()`: `RunCatalogue.Build(Document, Shop)`.

In `MainViewModel.RememberNumbering`, which already hands a new page the choices that outlive one
run, add the shop before the page is filled:

```csharp
        page.Shop = _saved.Shops.Count > 0
            ? _saved.Shops.Find(s => string.Equals(s.Name, _saved.PreferredShop, StringComparison.Ordinal))
              ?? _saved.Shops[0]
            : Shops.Defaults[0];
```

Rename that method to `RememberChoices`, since it is no longer only about numbering.

- [ ] **Step 7: Show the button**

In `src/Lego2STL.Gui/Views/CatalogueView.axaml`, beside the two buttons already on a card:

```xml
                  <Button Classes="link" IsVisible="{Binding CanBuy}"
                          Command="{Binding BuyCommand}"
                          Content="{Binding BuyText}" />
```

Match the classes and layout of the "Open the shape file" button already there.

- [ ] **Step 8: Run the whole suite**

```
dotnet test Lego2STL.slnx
```

Expected: PASS.

- [ ] **Step 9: Commit**

```bash
git add -A
git commit -m "feat: the catalogue offers to buy a part it could not print"
```

---

### Task 11: Measure the result on the real run and report it

**Files:**
- Modify: `docs/superpowers/specs/2026-08-30-not-printable-parts-design.md`

No code. It closes the loop the way Lot B's Task 4 did.

- [ ] **Step 1: Build the reference set again**

```bash
dotnet build src/Lego2STL.Cli/Lego2STL.Cli.csproj -c Debug
./src/Lego2STL.Cli/bin/Debug/net10.0-windows10.0.19041.0/lego2stl.exe build 6324712/6324712.csv --scale 200 --lang it --output-dir <scratchpad>/lotc
```

- [ ] **Step 2: Count what changed**

```
python -c "
import json
d=json.load(open(r'<scratchpad>/lotc/6324712/run.json',encoding='utf-8-sig'))
n=[p for p in d['parts'] if p.get('printability') in ('material','kind')]
print(len(n),'not printed:',sorted({p['part'] for p in n}))
print('plates:',d['plateCount'],'shapes:',d['shapeCount'])
"
```

Compare the plate count against the run made during Lot B: 124 plates, 175 shapes.

- [ ] **Step 3: Write the numbers into the spec**

Add a "What it actually did" section to
`docs/superpowers/specs/2026-08-30-not-printable-parts-design.md`, naming the parts left out and
the plates saved. If any part is left out that should have been printed, stop and report it
rather than trimming the rule to fit — a wrongly excluded part is the one failure mode this
design has, and `--print-everything` is its escape hatch, not a fix.

- [ ] **Step 4: Commit**

```bash
git add docs/superpowers/specs/2026-08-30-not-printable-parts-design.md
git commit -m "docs: record what leaving unprintable parts out actually saved"
```

---

## Notes for whoever executes this

- **Tasks 1 and 2 before everything.** Every other task consumes the verdict.
- **Task 5 before Task 6.** Task 6's constants are Task 5's output; building it first means
  inventing them.
- Tasks 7, 8 and 9 are independent of Tasks 5 and 6 and of each other. Task 10 needs 4, 6, 7
  and 8.
- The dump is not in the repository and never will be — it is not ours to redistribute. Every
  test builds its own fixture, and every path through the code works without it. If a test needs
  the real `DB Lego` folder to pass, it is the wrong test.
- Record `PHASE:LOT-C WAVE:0 STATUS:complete TS:<ISO-8601-UTC>` in `PROGRESS.md` when all eleven
  are done, and one `WAVE:<n>` line per task as you go.
