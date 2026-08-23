using FluentAssertions;
using Lego2STL.Core.Pdf;

namespace Lego2STL.Tests.Pdf;

public sealed class PageRangeTests
{
    [Fact]
    public void A_single_page_parses() =>
        PageRange.Parse("3").Should().Equal(3);

    [Fact]
    public void A_range_is_inclusive_at_both_ends() =>
        PageRange.Parse("2-5").Should().Equal(2, 3, 4, 5);

    [Fact]
    public void Comma_separated_sections_combine() =>
        PageRange.Parse("2-5,8,11-13").Should().Equal(2, 3, 4, 5, 8, 11, 12, 13);

    [Fact]
    public void Whitespace_around_sections_is_ignored() =>
        PageRange.Parse(" 2 - 5 , 8 ").Should().Equal(2, 3, 4, 5, 8);

    /// <summary>
    /// Overlapping input costs no extra work downstream, which is the point: each page is
    /// read once no matter how many times the range mentions it.
    /// </summary>
    [Fact]
    public void Overlapping_and_repeated_pages_collapse() =>
        PageRange.Parse("2-5,3,4,4-6").Should().Equal(2, 3, 4, 5, 6);

    [Fact]
    public void Sections_given_out_of_order_come_back_ascending() =>
        PageRange.Parse("9,2-3").Should().Equal(2, 3, 9);

    [Fact]
    public void A_page_beyond_the_document_is_rejected_naming_the_real_count()
    {
        var act = () => PageRange.Parse("2-5,200", pageCount: 126);

        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage("*page 200*only 126 pages*");
    }

    [Fact]
    public void The_page_count_check_is_skipped_when_the_count_is_unknown() =>
        PageRange.Parse("500").Should().Equal(500);

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void An_empty_range_is_rejected(string text)
    {
        var act = () => PageRange.Parse(text);

        act.Should().Throw<FormatException>().WithMessage("*empty*");
    }

    [Fact]
    public void An_empty_section_between_commas_is_rejected()
    {
        var act = () => PageRange.Parse("2-5,,8");

        act.Should().Throw<FormatException>().WithMessage("*empty section*");
    }

    [Theory]
    [InlineData("-5")]
    [InlineData("2-")]
    public void An_open_ended_range_is_rejected_rather_than_guessed(string text)
    {
        var act = () => PageRange.Parse(text);

        act.Should().Throw<FormatException>().WithMessage("*missing one end*");
    }

    [Fact]
    public void A_backwards_range_is_rejected_with_the_corrected_form()
    {
        var act = () => PageRange.Parse("5-2");

        act.Should().Throw<FormatException>().WithMessage("*runs backwards*'2-5'*");
    }

    [Theory]
    [InlineData("two")]
    [InlineData("2-x")]
    [InlineData("2.5")]
    [InlineData("+3")]
    public void Anything_that_is_not_a_page_number_is_rejected(string text)
    {
        var act = () => PageRange.Parse(text);

        act.Should().Throw<FormatException>().WithMessage("*not a page number*");
    }

    [Theory]
    [InlineData("0")]
    [InlineData("0-3")]
    public void Page_numbers_start_at_one(string text)
    {
        var act = () => PageRange.Parse(text);

        act.Should().Throw<FormatException>().WithMessage("*start at 1*");
    }

    [Theory]
    [InlineData(new[] { 2, 3, 4, 5 }, "2-5")]
    [InlineData(new[] { 3 }, "3")]
    [InlineData(new[] { 2, 3, 4, 5, 8, 11, 12, 13 }, "2-5,8,11-13")]
    [InlineData(new[] { 5, 2, 3, 4 }, "2-5")]
    [InlineData(new int[0], "")]
    public void Format_collapses_runs_back_into_the_shortest_expression(int[] pages, string expected) =>
        PageRange.Format(pages).Should().Be(expected);

    [Fact]
    public void Parse_and_Format_round_trip()
    {
        const string text = "2-5,8,11-13";

        PageRange.Format(PageRange.Parse(text)).Should().Be(text);
    }
}
