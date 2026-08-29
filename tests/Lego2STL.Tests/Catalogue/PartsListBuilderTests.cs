using FluentAssertions;
using Lego2STL.Core.Catalogue;
using Lego2STL.Core.Colors;
using Lego2STL.Core.Extraction;
using Lego2STL.Core.Ocr;

namespace Lego2STL.Tests.Catalogue;

public sealed class PartsListBuilderTests
{
    private static CatalogueReading Reading(string part, int colorCode, int quantity, int page = 2) =>
        new(page, new PixelBounds(0, 0, 1, 1), quantity, part, colorCode,
            ReadingSource.TextRecogniser, ReadingSource.TextRecogniser);

    private static PartsList Build(ColorScheme scheme, params CatalogueReading[] readings) =>
        PartsListBuilder.Build(readings, ColorReference.Table, scheme);

    /// <summary>
    /// An entry that came from an element number brings its own numbering, and mixing the two
    /// kinds in one list has to work: a document can print its catalogue on one page and have
    /// it read off the pixels on another.
    /// </summary>
    [Fact]
    public void An_entry_that_knows_its_own_numbering_is_not_read_in_the_run_s()
    {
        var readOffThePage = Reading("6628", 11, 1);                       // BrickLink black
        var fromAnElement = Reading("3707", 26, 2) with                     // LEGO black
        {
            Scheme = ColorScheme.Lego,
            QuantitySource = ReadingSource.PrintedText,
            PartSource = ReadingSource.PrintedText,
        };

        var list = PartsListBuilder.Build(
            [readOffThePage, fromAnElement], ColorReference.Table, ColorScheme.BrickLink);

        list.Entries.Should().OnlyContain(e => e.ColorName == "Black")
            .And.OnlyContain(e => e.BrickLinkColorCode == 11);
    }

    /// <summary>
    /// The run's numbering is still what an entry that does not know its own is read in, which
    /// is every entry read off the pixels.
    /// </summary>
    [Fact]
    public void An_entry_that_names_no_numbering_takes_the_run_s()
    {
        var list = Build(ColorScheme.Lego, Reading("3707", 26, 1));

        list.Entries.Single().ColorName.Should().Be("Black");
    }

    [Fact]
    public void Ids_run_from_one_in_the_order_the_entries_arrived()
    {
        var list = Build(
            ColorScheme.BrickLink,
            Reading("6628", 11, 1),
            Reading("32013", 11, 2),
            Reading("6632", 11, 3));

        list.Entries.Select(e => e.Id).Should().Equal(1, 2, 3);
        list.Entries.Select(e => e.PartNumber).Should().Equal("6628", "32013", "6632");
    }

    [Fact]
    public void Colour_numbers_are_resolved_to_a_name_and_a_value()
    {
        var entry = Build(ColorScheme.BrickLink, Reading("6628", 11, 1)).Entries.Single();

        entry.BrickLinkColorCode.Should().Be(11);
        entry.ColorName.Should().Be("Black");
        entry.Rgb.Should().Be(Rgb24.Parse("#05131D"));
    }

    /// <summary>
    /// The column always holds BrickLink's number, so lists made from differently numbered
    /// sources describe the same colour the same way.
    /// </summary>
    [Fact]
    public void A_source_using_another_numbering_is_translated_to_BrickLink()
    {
        // Rebrickable calls black 0; LDraw also calls it 0; BrickLink calls it 11.
        var fromRebrickable = Build(ColorScheme.Rebrickable, Reading("6628", 0, 1)).Entries.Single();
        var fromLego = Build(ColorScheme.Lego, Reading("6628", 26, 1)).Entries.Single();

        fromRebrickable.BrickLinkColorCode.Should().Be(11);
        fromRebrickable.ColorName.Should().Be("Black");
        fromLego.BrickLinkColorCode.Should().Be(11);
    }

    /// <summary>
    /// The same part in the same colour read twice - from an overlapping page range, or
    /// because it really is printed twice - becomes one row with the quantities added.
    /// </summary>
    [Fact]
    public void The_same_part_and_colour_twice_becomes_one_row_with_the_quantities_added()
    {
        var list = Build(
            ColorScheme.BrickLink,
            Reading("32054", 11, 2),
            Reading("32054", 11, 3));

        list.Entries.Should().HaveCount(1);
        list.Entries.Single().Quantity.Should().Be(5);
        list.Notes.Should().Contain(n => n.Contains("appears more than once"));
    }

    /// <summary>
    /// The same part in different colours stays separate: they are different things to buy,
    /// even though they are the same shape.
    /// </summary>
    [Fact]
    public void The_same_part_in_different_colours_stays_as_separate_rows()
    {
        var list = Build(
            ColorScheme.BrickLink,
            Reading("32054", 11, 2),
            Reading("32054", 9, 1),
            Reading("32054", 5, 3));

        list.Entries.Should().HaveCount(3);
        list.Entries.Select(e => e.Quantity).Should().Equal(2, 1, 3);
        list.DistinctPartNumbers.Should().Equal("32054");
    }

    [Fact]
    public void A_part_number_differing_only_in_case_is_the_same_part()
    {
        var list = Build(
            ColorScheme.BrickLink,
            Reading("4265C", 9, 4),
            Reading("4265c", 9, 6));

        list.Entries.Should().HaveCount(1);
        list.Entries.Single().Quantity.Should().Be(10);
    }

    [Fact]
    public void Total_pieces_counts_every_copy()
    {
        var list = Build(
            ColorScheme.BrickLink,
            Reading("2780", 11, 15),
            Reading("32556", 7, 38));

        list.TotalPieces.Should().Be(53);
    }

    [Fact]
    public void An_unknown_colour_number_is_reported_with_the_page_and_part()
    {
        var act = () => Build(ColorScheme.BrickLink, Reading("6628", 99999, 1));

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Page 2*6628*99999*--color-scheme*");
    }

    [DocumentFact]
    public void The_reference_document_yields_the_expected_list_shape()
    {
        var readings = ExpectedCatalogue.Entries
            .Select(e => Reading(e.PartNumber, e.ColorCode, e.Quantity, e.Page))
            .ToArray();

        var list = Build(ColorScheme.BrickLink, readings);

        list.Entries.Should().HaveCount(ExpectedCatalogue.Entries.Count,
            "no two entries of this catalogue are the same part in the same colour");
        list.DistinctPartNumbers.Should().HaveCount(ExpectedCatalogue.DistinctPartNumbers.Count);
        list.Entries.Select(e => e.Id).Should().BeInAscendingOrder();
    }

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
                ReadingSource.TextRecogniser, ReadingSource.TextRecogniser),
        };

        var list = PartsListBuilder.Build(readings, ColorReference.Table, ColorScheme.BrickLink);

        list.Entries.Should().ContainSingle().Which.ElementId.Should().BeNull();
    }
}
