using System;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using FluentAssertions;
using Lego2STL.Core.Catalogue;
using Lego2STL.Core.Colors;
using Lego2STL.Core.Pipeline;
using Lego2STL.Core.Run;
using Lego2STL.Gui.ViewModels;
using Lego2STL.Gui.Views;

namespace Lego2STL.UiTests;

/// <summary>
/// Being asked about what the reader could not make out.
/// </summary>
/// <remarks>
/// Three buttons and three quite different meanings: an answer corrects the list, a skip leaves
/// the question for later, and "not an entry" says the region was never a label at all and must
/// never be asked about again - including by a second run over the same pages.
/// </remarks>
public sealed class ReviewQuestionTests : IDisposable
{
    private readonly string _folder = Path.Combine(
        Path.GetTempPath(), "lego2stl-review-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    /// <summary>A run that has happened, holding whatever it could not read.</summary>
    private RunDocumentViewModel APretendRun(params ManifestUnread[] unread)
    {
        var layout = RunLayout.At(Path.Combine(_folder, "run"));

        var outcome = new RunOutcome
        {
            Result = unread.Length > 0 ? RunResult.Unverified : RunResult.Complete,
            Settings = new RunSettings { Kind = InputKind.PartsList, InputPath = "run.csv", Offline = true },
            Layout = layout,
            PartsList = new PartsList(
                [new PartEntry(1, "3705", 5, "Red", Rgb24.Parse("#C91A09"), 12)], []),
        };

        var manifest = RunManifest.From(
            outcome, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null) with
        {
            Unread = [.. unread],
        };

        return RunDocumentViewModel.Of(RunDocument.From(manifest, layout));
    }

    private RunDocumentViewModel ARunWithAnUnreadEntry() => APretendRun(
        new ManifestUnread(372, "(444,530)-(512,566)", "7x 6177114", 7, null, null, "could not be read"));

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

    /// <summary>
    /// A question already answered is not asked a second time, whoever opens the run.
    /// </summary>
    /// <remarks>
    /// This is what the answers file is for, and what makes a second run over the same pages
    /// stay quiet about a region someone has already said is not a label.
    /// </remarks>
    [AvaloniaFact]
    public void A_region_already_answered_is_never_asked_about_again()
    {
        using var first = ARunWithAnUnreadEntry();
        first.CurrentQuestion!.NotAnEntryCommand.Execute(null);

        using var reopened = ARunWithAnUnreadEntry();

        reopened.HasQuestions.Should().BeFalse();
    }

    /// <summary>
    /// The panel is actually drawn, with all three buttons on it.
    /// </summary>
    /// <remarks>
    /// Drawn rather than reasoned about, because a mistyped binding costs nothing at build time
    /// and shows up only as a panel that is blank or missing on screen.
    /// </remarks>
    [AvaloniaFact]
    public void The_question_is_drawn_above_the_cards()
    {
        using var run = ARunWithAnUnreadEntry();

        var window = new Window
        {
            Width = 1000,
            Height = 700,
            Content = new RunDocumentView { DataContext = run },
        };

        window.Show();

        window.CaptureRenderedFrame().Should().NotBeNull();

        var buttons = window.GetVisualDescendants().OfType<Button>().Select(b => b.Name).ToList();

        buttons.Should().Contain("AnswerOk").And.Contain("AnswerSkip").And.Contain("AnswerNotALegoCode");
    }

    /// <summary>An answer lands in the parts list on the disk, which is what is continued from.</summary>
    [AvaloniaFact]
    public void An_answer_is_written_into_the_parts_list_itself()
    {
        using var run = ARunWithAnUnreadEntry();

        var question = run.CurrentQuestion!;
        question.PartNumber = "32523";
        question.ColorCode = 11;
        question.Quantity = 4;
        question.AcceptCommand.Execute(null);

        File.ReadAllText(run.Document.PartsListPath).Should().Contain("32523");
    }
}
