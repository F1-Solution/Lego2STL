# Answering What The Reader Could Not Make Out — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let a person answer, from the catalogue, the entries the reader could not decipher —
seeing the piece of page in question, typing the part, colour and quantity, or saying it was never
an entry at all — and have the answer correct the parts list.

**Architecture:** Three steps. First the record stops keeping unread entries as finished
sentences and keeps them as data, which is what nothing downstream can work without. Then the run
saves a crop of each unread region, so there is something to look at. Then the catalogue grows a
panel that asks, writes the answers into the run's own `overrides.csv`, and corrects the parts
list — after which the way back to shapes and plates is the *continue from the parts list* road
that already exists.

**Tech Stack:** C# / .NET 10, xUnit + FluentAssertions, Avalonia 12.1.1 (headless for UI tests),
CommunityToolkit.Mvvm, SkiaSharp.

**Spec:** `docs/superpowers/specs/2026-08-30-lot-d-what-is-left.md`, item 6, including the four
decisions recorded there.

## Global Constraints

- Build with `dotnet build Lego2STL.slnx -c Debug`. Test with `dotnet test Lego2STL.slnx`.
- Every user-facing string goes through `TextKey` and is added to **both**
  `Strings.English.cs` and `Strings.Italian.cs`.
- Code comments and CHANGELOG entries: **one sentence each**. Test comments are exempt.
- Commit messages: `<type>: <description>`, describing observable behaviour, never internal
  class or method names.
- Files stay under 800 lines; functions under 50.
- The parts-list CSV keeps its six columns. An answer adds a row; it never adds a column.
- **The run never stops to ask.** Nothing in this plan may make the pipeline wait on a person.
- **A record written before this must still open.** Every field added is optional, and a manifest
  without it reads as a run with nothing to answer.
- Answers live in the run's own folder, so they belong to that document and travel with it.

---

### Task 1: The record keeps unread entries as data

**Files:**
- Modify: `src/Lego2STL.Core/Run/RunManifest.cs:132` and `:225-228`
- Modify: `src/Lego2STL.Core/Run/RunDocument.cs:130` and `:197`
- Modify: `src/Lego2STL.Core/Pipeline/RunReport.cs` (wherever it prints the unread list)
- Test: `tests/Lego2STL.Tests/Run/RunManifestTests.cs`

**Interfaces:**
- Consumes: `UnresolvedReading(int Page, PixelBounds Bounds, string RawText, int? Quantity,
  string? PartNumber, int? ColorCode, string Reason)`, unchanged.
- Produces: `ManifestUnread(int Page, string Bounds, string RawText, int? Quantity,
  string? PartNumber, int? ColorCode, string Reason)` and `RunManifest.Unread` /
  `RunDocument.Unread` retyped to `IReadOnlyList<ManifestUnread>`; `RunDocument.UnreadText`
  (`IReadOnlyList<string>`) keeps the sentences the window already shows, formatted on demand in
  the current language rather than at the moment the run ended.

This is the same correction Lot B made to the list of parts too big for the plate: a finished
sentence is where information goes to die.

- [ ] **Step 1: Write the failing test**

Add to `tests/Lego2STL.Tests/Run/RunManifestTests.cs`:

```csharp
    /// <summary>
    /// An entry that could not be read is kept with its page and its region, not as a sentence.
    /// </summary>
    /// <remarks>
    /// Kept as a sentence, nothing could show the piece of page in question or ask about it -
    /// and the sentence was formatted in whatever language the run spoke, so a window switched
    /// to the other one showed the old words for ever.
    /// </remarks>
    [Fact]
    public void An_entry_that_could_not_be_read_keeps_its_page_and_its_region()
    {
        var layout = ARunFolder();
        var language = DisplayLanguage.Italian;

        var manifest = RunManifest.From(
            APretendRun.WithAnUnreadEntry(layout, language),
            APretendRun.Started,
            APretendRun.Finished,
            null);

        var unread = manifest.Unread.Should().ContainSingle().Subject;

        unread.Page.Should().BePositive();
        unread.Bounds.Should().NotBeNullOrWhiteSpace();
        unread.RawText.Should().NotBeNull();

        var document = RunDocument.From(manifest, layout);

        document.Unread.Should().ContainSingle();
        document.UnreadText.Should().ContainSingle().Which.Should().Contain(unread.Page.ToString());
    }
```

- [ ] **Step 2: Run it and watch it fail**

```
dotnet test tests/Lego2STL.Tests/Lego2STL.Tests.csproj --filter "FullyQualifiedName~keeps_its_page_and_its_region"
```

Expected: FAIL to compile — `Unread` is a list of strings, and `UnreadText` does not exist.

- [ ] **Step 3: Add the record and retype the field**

In `src/Lego2STL.Core/Run/RunManifest.cs`, beside `ManifestFailure`:

```csharp
/// <summary>
/// An entry the reader could not make out, with enough to ask a person about it.
/// </summary>
/// <param name="Bounds">
/// The region on the page, as the same text the extraction prints, so the file stays readable
/// by eye and the shape of <c>PixelBounds</c> is not frozen into it.
/// </param>
public sealed record ManifestUnread(
    int Page,
    string Bounds,
    string RawText,
    int? Quantity,
    string? PartNumber,
    int? ColorCode,
    string Reason);
```

Retype the field:

```csharp
    /// <summary>Entries the reader could not make out, each with where it was.</summary>
    public IReadOnlyList<ManifestUnread> Unread { get; init; } = [];
```

and replace the projection at lines 225-228:

```csharp
            Unread =
            [
                .. outcome.Unread.Select(u => new ManifestUnread(
                    u.Page, u.Bounds.ToString()!, u.RawText, u.Quantity, u.PartNumber, u.ColorCode, u.Reason)),
            ],
```

- [ ] **Step 4: Carry it to the document, keeping the sentences**

In `src/Lego2STL.Core/Run/RunDocument.cs`:

```csharp
    /// <summary>Entries the reader could not make out, each with where it was.</summary>
    public IReadOnlyList<ManifestUnread> Unread { get; init; } = [];

    /// <summary>
    /// The same entries as sentences, for the places that only show them.
    /// </summary>
    /// <remarks>
    /// Worded on demand rather than when the run ended, so switching the window's language
    /// re-words them instead of leaving the old ones in place for ever.
    /// </remarks>
    public IReadOnlyList<string> UnreadText => [.. Unread.Select(u =>
        Strings.For(Language).Format(TextKey.MsgUnreadEntryAt, u.Page, u.Bounds, u.Reason))];
```

If `RunDocument` has no `Language`, use `DisplayLanguages.Fallback` and let the window's own
wording layer re-word it; do not add a language to the document for this alone.

Update the projection at line 197 to `Unread = manifest.Unread,`.

- [ ] **Step 5: Fix every reader of the old shape**

Search for uses and give each `UnreadText`:

```
grep -rn "\.Unread" --include=*.cs --include=*.axaml src/ tests/ | grep -v "/bin/\|/obj/"
```

`RunDocumentViewModel.ProblemText` and `RunReport` both consume it. Neither should keep building
its own sentence.

- [ ] **Step 6: Run the whole suite**

```
dotnet test Lego2STL.slnx
```

Expected: PASS.

- [ ] **Step 7: Commit**

```bash
git add -A
git commit -m "fix: an entry that could not be read keeps its page and its region"
```

---

### Task 2: The run keeps a picture of what it could not read

**Files:**
- Modify: `src/Lego2STL.Core/Pipeline/PipelineRunner.cs` (the reading stages)
- Modify: `src/Lego2STL.Core/Run/RunManifest.cs` (`ManifestUnread`)
- Test: `tests/Lego2STL.Tests/Extraction/PartPictureTests.cs`

**Interfaces:**
- Consumes: `RowCrop.Extract`, `RowCrop.ToPng`, `RunLayout.ReviewDirectory` — declared long ago
  as *"crops of anything that could not be read, for checking by eye"* and used by nothing.
- Produces: a PNG per unread entry in the run's `review/` folder, named
  `p<page>-<left>-<top>.png`, and `ManifestUnread.Picture` (`string?`) holding that file name.

- [ ] **Step 1: Write the failing test**

Add to `tests/Lego2STL.Tests/Extraction/PartPictureTests.cs`:

```csharp
    /// <summary>What could not be read is kept as a picture, to be looked at afterwards.</summary>
    [Fact]
    public void A_region_that_could_not_be_read_is_saved_to_be_looked_at()
    {
        using var page = APage();

        var name = PartPicture.WriteReviewCrop(page, new PixelBounds(160, 200, 240, 230), _folder, 370);

        name.Should().NotBeNull();
        File.Exists(Path.Combine(_folder, name!)).Should().BeTrue();
        name.Should().Contain("370", "the page it came from is part of how it is found again");
    }

    /// <summary>Two regions on one page do not overwrite each other.</summary>
    [Fact]
    public void Two_regions_on_the_same_page_are_kept_apart()
    {
        using var page = APage();

        var first = PartPicture.WriteReviewCrop(page, new PixelBounds(10, 10, 40, 30), _folder, 370);
        var second = PartPicture.WriteReviewCrop(page, new PixelBounds(60, 10, 90, 30), _folder, 370);

        second.Should().NotBe(first);
    }
```

This task assumes `PartPicture` exists from Lot C. If Lot C has not been built, create the file
with just this one method; Lot C's Task 6 will then add the rest to it.

- [ ] **Step 2: Run it and watch it fail**

```
dotnet test tests/Lego2STL.Tests/Lego2STL.Tests.csproj --filter "FullyQualifiedName~could_not_be_read_is_saved"
```

Expected: FAIL to compile.

- [ ] **Step 3: Write it**

In `src/Lego2STL.Core/Extraction/PartPicture.cs`:

```csharp
    /// <summary>
    /// Saves the region an entry could not be read from, and gives back the file's name.
    /// </summary>
    /// <remarks>
    /// Named after the page and the corner it came from, so the same region read again lands on
    /// the same file rather than filling the folder with copies.
    /// </remarks>
    public static string? WriteReviewCrop(SKBitmap page, PixelBounds region, string directory, int pageNumber)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);

        var name = $"p{pageNumber}-{region.Left}-{region.Top}.png";

        try
        {
            using var crop = RowCrop.Extract(page, region);

            Directory.CreateDirectory(directory);
            File.WriteAllBytes(Path.Combine(directory, name), RowCrop.ToPng(crop));
            return name;
        }
        catch (Exception ex) when (ex is IOException
                                       or UnauthorizedAccessException
                                       or InvalidOperationException)
        {
            return null;
        }
    }
```

The white margin `RowCrop` adds by default is wanted here: a person reads a crop better with
space around it, for the same reason the recogniser did.

- [ ] **Step 4: Call it where a reading fails**

In `PipelineRunner`, wherever an `UnresolvedReading` is added — both reading stages have one —
write the crop first and keep its name on the reading. Add `Picture` to `UnresolvedReading` as a
trailing optional `string?`, carry it into `ManifestUnread` as a trailing optional `string?
Picture = null`, and into `RunDocument` with it.

- [ ] **Step 5: Run the whole suite, then look**

```
dotnet test Lego2STL.slnx
dotnet run --project src/Lego2STL.Cli -- build 6324712.pdf --pages 372 --lang it
```

Page 372 is the page the report says has an entry it cannot decode. Open the PNG in the run's
`review/` folder: it must show the label a person is being asked about, right way up and readable.

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "feat: a run keeps a picture of every entry it could not read"
```

---

### Task 3: The answers, and remembering them

**Files:**
- Create: `src/Lego2STL.Core/Run/ReviewAnswers.cs`
- Test: `tests/Lego2STL.Tests/Run/ReviewAnswersTests.cs` (create)

**Interfaces:**
- Consumes: `RunLayout.OverridesPath`, declared as *"answers given during review, so the same
  question is not asked twice"* and used by nothing.
- Produces: `sealed record ReviewAnswer(int Page, string Bounds, string? PartNumber, int? ColorCode,
  int? Quantity, bool NotAnEntry)` and `static class ReviewAnswers` with
  `Read(string path) → IReadOnlyList<ReviewAnswer>`, `Append(string path, ReviewAnswer answer)`,
  and `Key(int page, string bounds) → string`.

- [ ] **Step 1: Write the failing tests**

Create `tests/Lego2STL.Tests/Run/ReviewAnswersTests.cs`:

```csharp
using FluentAssertions;
using Lego2STL.Core.Run;

namespace Lego2STL.Tests.Run;

/// <summary>
/// The answers a person gave about what the reader could not make out.
/// </summary>
/// <remarks>
/// They live in the run's own folder because they are about one document, and a second run over
/// the same pages lands in the same folder - which is exactly when not asking twice matters.
/// </remarks>
public sealed class ReviewAnswersTests : IDisposable
{
    private readonly string _folder = Path.Combine(
        Path.GetTempPath(), "lego2stl-answers-" + Guid.NewGuid().ToString("N"));

    private string Path_ => System.IO.Path.Combine(_folder, "overrides.csv");

    public ReviewAnswersTests() => Directory.CreateDirectory(_folder);

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    [Fact]
    public void An_answer_survives_being_written_and_read_back()
    {
        var answer = new ReviewAnswer(372, "(100,200)-(140,230)", "32523", 11, 4, NotAnEntry: false);

        ReviewAnswers.Append(Path_, answer);

        ReviewAnswers.Read(Path_).Should().ContainSingle().Which.Should().Be(answer);
    }

    [Fact]
    public void Saying_it_was_never_an_entry_is_also_an_answer()
    {
        ReviewAnswers.Append(Path_, new ReviewAnswer(372, "(0,0)-(10,10)", null, null, null, true));

        ReviewAnswers.Read(Path_).Should().ContainSingle().Which.NotAnEntry.Should().BeTrue();
    }

    /// <summary>A region answered twice keeps the later answer, so a mistake can be corrected.</summary>
    [Fact]
    public void Answering_the_same_region_again_replaces_the_first_answer()
    {
        ReviewAnswers.Append(Path_, new ReviewAnswer(372, "(0,0)-(10,10)", "3705", 5, 1, false));
        ReviewAnswers.Append(Path_, new ReviewAnswer(372, "(0,0)-(10,10)", "32523", 11, 2, false));

        var kept = ReviewAnswers.Read(Path_).Should().ContainSingle().Subject;

        kept.PartNumber.Should().Be("32523");
        kept.Quantity.Should().Be(2);
    }

    [Fact]
    public void No_file_at_all_is_no_answers_and_no_complaint()
    {
        ReviewAnswers.Read(System.IO.Path.Combine(_folder, "nothing.csv")).Should().BeEmpty();
    }

    /// <summary>A file edited by hand into nonsense loses the answers, not the run.</summary>
    [Fact]
    public void A_file_that_cannot_be_understood_is_treated_as_no_answers()
    {
        File.WriteAllText(Path_, "this is not a csv\nnor is this\n");

        var read = () => ReviewAnswers.Read(Path_);

        read.Should().NotThrow();
    }

    /// <summary>Two regions on one page are two questions, not one.</summary>
    [Fact]
    public void A_region_is_told_from_another_on_the_same_page()
    {
        ReviewAnswers.Key(372, "(0,0)-(10,10)").Should().NotBe(ReviewAnswers.Key(372, "(20,0)-(30,10)"));
    }
}
```

- [ ] **Step 2: Run them and watch them fail**

```
dotnet test tests/Lego2STL.Tests/Lego2STL.Tests.csproj --filter "FullyQualifiedName~ReviewAnswers"
```

Expected: FAIL to compile.

- [ ] **Step 3: Write it**

Create `src/Lego2STL.Core/Run/ReviewAnswers.cs`. Write the file with a header row and one row per
answer — `page,bounds,part,color,quantity,notAnEntry` — reusing the same minimal CSV splitting the
rest of the project uses rather than taking a dependency. Reading keeps the **last** row for each
key, which is what makes a correction a correction. Reading never throws: a file that cannot be
understood is no answers.

- [ ] **Step 4: Run the tests**

```
dotnet test tests/Lego2STL.Tests/Lego2STL.Tests.csproj --filter "FullyQualifiedName~ReviewAnswers"
```

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Lego2STL.Core/Run/ReviewAnswers.cs tests/Lego2STL.Tests/Run/ReviewAnswersTests.cs
git commit -m "feat: answers about unread entries are kept with the run they belong to"
```

---

### Task 4: An answer corrects the parts list

**Files:**
- Create: `src/Lego2STL.Core/Catalogue/PartsListCorrection.cs`
- Test: `tests/Lego2STL.Tests/Catalogue/PartsListCorrectionTests.cs` (create)

**Interfaces:**
- Consumes: `ReviewAnswer`, `PartsList`, `PartEntry`, `ColorReference.Table`.
- Produces: `PartsListCorrection.Apply(PartsList list, IReadOnlyList<ReviewAnswer> answers) → PartsList`
  — the list with each answer's entry added or its quantity increased, renumbered, and with
  answers marked *not an entry* ignored.

- [ ] **Step 1: Write the failing tests**

Create `tests/Lego2STL.Tests/Catalogue/PartsListCorrectionTests.cs`:

```csharp
using FluentAssertions;
using Lego2STL.Core.Catalogue;
using Lego2STL.Core.Colors;
using Lego2STL.Core.Run;

namespace Lego2STL.Tests.Catalogue;

/// <summary>
/// Folding a person's answers back into the parts list.
/// </summary>
/// <remarks>
/// The rule that matters is the one about a part already on the list: an answer that names a
/// part in a colour that is already there adds to that row rather than making a second one,
/// because the same part in the same colour is one thing to buy however many labels printed it.
/// </remarks>
public sealed class PartsListCorrectionTests
{
    private static PartsList AListOf(params (string Part, int Color, int Quantity)[] rows) =>
        new([.. rows.Select((r, i) => new PartEntry(
            i + 1, r.Part, r.Color, "Black", Rgb24.Parse("#05131D"), r.Quantity))], []);

    [Fact]
    public void An_answer_adds_the_part_it_names()
    {
        var list = AListOf(("32523", 11, 4));

        var corrected = PartsListCorrection.Apply(
            list, [new ReviewAnswer(372, "(0,0)-(1,1)", "3705", 5, 2, false)]);

        corrected.Entries.Should().HaveCount(2);
        corrected.Entries.Should().Contain(e => e.PartNumber == "3705" && e.Quantity == 2);
    }

    [Fact]
    public void An_answer_naming_a_part_already_there_adds_to_its_row()
    {
        var list = AListOf(("32523", 11, 4));

        var corrected = PartsListCorrection.Apply(
            list, [new ReviewAnswer(372, "(0,0)-(1,1)", "32523", 11, 3, false)]);

        corrected.Entries.Should().ContainSingle().Which.Quantity.Should().Be(7);
    }

    /// <summary>The same part in another colour is another row, as it always was.</summary>
    [Fact]
    public void The_same_part_in_another_colour_is_another_row()
    {
        var list = AListOf(("32523", 11, 4));

        var corrected = PartsListCorrection.Apply(
            list, [new ReviewAnswer(372, "(0,0)-(1,1)", "32523", 5, 3, false)]);

        corrected.Entries.Should().HaveCount(2);
    }

    [Fact]
    public void An_answer_that_says_it_was_never_an_entry_changes_nothing()
    {
        var list = AListOf(("32523", 11, 4));

        var corrected = PartsListCorrection.Apply(
            list, [new ReviewAnswer(372, "(0,0)-(1,1)", null, null, null, true)]);

        corrected.Entries.Should().ContainSingle().Which.Quantity.Should().Be(4);
    }

    [Fact]
    public void An_answer_missing_what_it_needs_changes_nothing()
    {
        var list = AListOf(("32523", 11, 4));

        var corrected = PartsListCorrection.Apply(
            list, [new ReviewAnswer(372, "(0,0)-(1,1)", "3705", null, 2, false)]);

        corrected.Entries.Should().ContainSingle();
    }

    /// <summary>The numbering stays 1..N and in order, because the file is read by people too.</summary>
    [Fact]
    public void The_rows_are_numbered_again_from_one()
    {
        var list = AListOf(("32523", 11, 4), ("2780", 7, 20));

        var corrected = PartsListCorrection.Apply(
            list, [new ReviewAnswer(372, "(0,0)-(1,1)", "3705", 5, 2, false)]);

        corrected.Entries.Select(e => e.Id).Should().Equal(1, 2, 3);
    }
}
```

- [ ] **Step 2: Run them and watch them fail**

```
dotnet test tests/Lego2STL.Tests/Lego2STL.Tests.csproj --filter "FullyQualifiedName~PartsListCorrection"
```

Expected: FAIL to compile.

- [ ] **Step 3: Write it**

Create `src/Lego2STL.Core/Catalogue/PartsListCorrection.cs`. It folds each usable answer into a
copy of the list, matching on `PartEntry.Key` — which is already *(part number lowered, colour)*
and already the project's answer to "is this the same row" — then renumbers. An answer marked
*not an entry*, or missing a part number, a colour or a quantity, is skipped: the dialogue is
free to record a partial answer, and this is where partial means nothing changes. The colour's
name and value come from `ColorReference.Table` for the BrickLink code given.

- [ ] **Step 4: Run the tests, then the suite**

```
dotnet test Lego2STL.slnx
```

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat: an answer about an unread entry corrects the parts list"
```

---

### Task 5: The catalogue asks

**Files:**
- Create: `src/Lego2STL.Gui/ViewModels/ReviewQuestionViewModel.cs`
- Modify: `src/Lego2STL.Gui/ViewModels/RunDocumentViewModel.cs`
- Modify: `src/Lego2STL.Gui/Views/CatalogueView.axaml`
- Modify: `src/Lego2STL.Core/Text/TextKey.cs`, `Strings.English.cs`, `Strings.Italian.cs`
- Test: `tests/Lego2STL.UiTests/ReviewQuestionTests.cs` (create)

**Interfaces:**
- Consumes: `RunDocument.Unread` (Task 1), the crop names (Task 2), `ReviewAnswers` (Task 3),
  `PartsListCorrection` (Task 4).
- Produces: `ReviewQuestionViewModel` with `Picture`, `RawText`, `PartNumber`, `ColorCode`,
  `Quantity`, `AcceptCommand`, `SkipCommand`, `NotAnEntryCommand`; on `RunDocumentViewModel`,
  `Questions` (`ObservableCollection<ReviewQuestionViewModel>`), `CurrentQuestion`, and
  `HasQuestions`.

- [ ] **Step 1: Write the failing tests**

Create `tests/Lego2STL.UiTests/ReviewQuestionTests.cs`:

```csharp
using FluentAssertions;
using Avalonia.Headless.XUnit;
using Lego2STL.Gui.ViewModels;

namespace Lego2STL.UiTests;

/// <summary>
/// Being asked about what the reader could not make out.
/// </summary>
/// <remarks>
/// Three buttons and three quite different meanings: an answer corrects the list, a skip leaves
/// the question for later, and "not an entry" says the region was never a label at all and must
/// never be asked about again - including by a second run over the same pages.
/// </remarks>
public sealed class ReviewQuestionTests
{
    [AvaloniaFact]
    public void A_run_with_nothing_unread_asks_nothing()
    {
        using var run = APretendRun();

        run.HasQuestions.Should().BeFalse();
    }

    [AvaloniaFact]
    public void A_run_with_an_unread_entry_asks_about_it()
    {
        using var run = ARunWithAnUnreadEntry();

        run.HasQuestions.Should().BeTrue();
        run.CurrentQuestion.Should().NotBeNull();
        run.CurrentQuestion!.RawText.Should().NotBeNull();
    }

    [AvaloniaFact]
    public void An_answer_is_kept_and_the_question_is_not_asked_again()
    {
        using var run = ARunWithAnUnreadEntry();

        var question = run.CurrentQuestion!;
        question.PartNumber = "32523";
        question.ColorCode = 11;
        question.Quantity = 4;
        question.AcceptCommand.Execute(null);

        run.Questions.Should().NotContain(question);
        run.Parts.Should().Contain(p => p.PartNumber == "32523");
    }

    [AvaloniaFact]
    public void An_answer_that_is_incomplete_is_not_accepted()
    {
        using var run = ARunWithAnUnreadEntry();

        var question = run.CurrentQuestion!;
        question.PartNumber = "32523";
        question.AcceptCommand.Execute(null);

        run.Questions.Should().Contain(question, "a part with no colour is not an answer yet");
    }

    [AvaloniaFact]
    public void Skipping_leaves_the_question_for_another_day()
    {
        using var run = ARunWithAnUnreadEntry();
        var question = run.CurrentQuestion!;

        question.SkipCommand.Execute(null);

        run.Questions.Should().Contain(question);
        run.CurrentQuestion.Should().NotBe(question, "the next one is offered instead");
    }

    [AvaloniaFact]
    public void Saying_it_was_never_an_entry_takes_the_question_away_for_good()
    {
        using var run = ARunWithAnUnreadEntry();
        var question = run.CurrentQuestion!;

        question.NotAnEntryCommand.Execute(null);

        run.Questions.Should().NotContain(question);
        run.Parts.Should().NotContain(p => p.PartNumber == "32523");
    }
}
```

Write `ARunWithAnUnreadEntry()` beside the other pretend runs in `CatalogueTests.cs`, following
`APretendRun.WithAnUnreadEntry`, and give the run folder a real `overrides.csv` path so the
answer has somewhere to go. The last test's assertion about `Parts` is the one that proves *not
an entry* did not quietly add a row.

- [ ] **Step 2: Run them and watch them fail**

```
dotnet test tests/Lego2STL.UiTests/Lego2STL.UiTests.csproj --filter "FullyQualifiedName~ReviewQuestion"
```

Expected: FAIL to compile.

- [ ] **Step 3: Add the wording**

`TextKey.cs`:

```csharp
    /// <summary>The panel that asks about an entry the reader could not make out.</summary>
    UiCouldNotRead,

    /// <summary>Asks for the part number of the piece in the picture.</summary>
    UiWhichPart,

    /// <summary>Asks for its colour code.</summary>
    UiWhichColour,

    /// <summary>Asks how many.</summary>
    UiHowMany,

    /// <summary>Accepts the answer.</summary>
    UiAnswerOk,

    /// <summary>Leaves the question for later.</summary>
    UiAnswerSkip,

    /// <summary>Says the region was never an entry at all.</summary>
    UiAnswerNotALegoCode,
```

`Strings.English.cs`:

```csharp
            [TextKey.UiCouldNotRead] = "This could not be read. What is it?",
            [TextKey.UiWhichPart] = "Part number",
            [TextKey.UiWhichColour] = "Colour code",
            [TextKey.UiHowMany] = "How many",
            [TextKey.UiAnswerOk] = "Ok",
            [TextKey.UiAnswerSkip] = "Skip",
            [TextKey.UiAnswerNotALegoCode] = "Not a LEGO code",
```

`Strings.Italian.cs`:

```csharp
            [TextKey.UiCouldNotRead] = "Questo non è stato letto. Che cos'è?",
            [TextKey.UiWhichColour] = "Codice colore",
            [TextKey.UiWhichPart] = "Codice pezzo",
            [TextKey.UiHowMany] = "Quanti",
            [TextKey.UiAnswerOk] = "Ok",
            [TextKey.UiAnswerSkip] = "Salta",
            [TextKey.UiAnswerNotALegoCode] = "Non è un codice Lego",
```

- [ ] **Step 4: Write the question**

Create `src/Lego2STL.Gui/ViewModels/ReviewQuestionViewModel.cs` holding one unread entry, its
crop loaded from the run's `review/` folder, and three commands. `Accept` refuses an answer
missing any of the three fields — the same rule `PartsListCorrection` applies, checked here so
the button can say so rather than doing nothing. Each of `Accept` and `NotAnEntry` appends to
`overrides.csv` and tells the page it is done; `Skip` only moves on.

- [ ] **Step 5: Put it on the page**

`RunDocumentViewModel` builds the questions from `Document.Unread`, dropping any whose region
already has an answer in `overrides.csv` — that is what *"the same question is not asked twice"*
means, and it is why a second run over the same pages stays quiet. Answering writes the corrected
parts list back with `PartsListCsv.WriteFileAsync` and re-reads the catalogue from it.

In `CatalogueView.axaml`, put the panel above the cards, in the place Lot B's "some parts do not
fit" band occupies, and visible on `HasQuestions`: the crop, the raw text, three inputs, three
buttons.

- [ ] **Step 6: Run the whole suite**

```
dotnet test Lego2STL.slnx
```

Expected: PASS.

- [ ] **Step 7: Drive it by hand**

Build `6324712.pdf` over pages 370-372, open the run in the window, and answer the question page
372 raises. Check three things by eye: the crop shows what is actually being asked about; the
corrected part appears in the catalogue; and reopening the run does not ask again.

- [ ] **Step 8: Commit**

```bash
git add -A
git commit -m "feat: the catalogue asks about entries the reader could not make out"
```

---

## Notes for whoever executes this

- **Task 1 before everything.** Nothing can be asked about a sentence.
- Task 2 reuses Lot C's `PartPicture`. If Lot C has not been built, this plan creates the file
  and Lot C's Task 6 extends it; the two do not conflict.
- Tasks 3 and 4 are independent of Task 2 and of each other.
- The pipeline must not learn to wait. If a task starts pulling the dialogue into the run, stop:
  the decision that it is asked afterwards is the decision that keeps a long run finishing on its
  own.
- Record `PHASE:LOT-D WAVE:6 STATUS:complete TS:<ISO-8601-UTC>` in `PROGRESS.md` when all five
  are done, and one `WAVE:6.<n>` line per task as you go.
