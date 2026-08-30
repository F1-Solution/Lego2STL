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
