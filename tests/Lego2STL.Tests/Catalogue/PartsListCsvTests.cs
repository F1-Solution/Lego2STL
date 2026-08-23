using FluentAssertions;
using Lego2STL.Core.Catalogue;
using Lego2STL.Core.Colors;

namespace Lego2STL.Tests.Catalogue;

public sealed class PartsListCsvTests
{
    private static readonly PartEntry Black6628 =
        new(1, "6628", 11, "Black", Rgb24.Parse("#05131D"), 1);

    private static readonly PartEntry Grey4265C =
        new(2, "4265c", 9, "Light Gray", Rgb24.Parse("#9BA19D"), 10);

    private static readonly PartEntry Red32556 =
        new(3, "32556", 5, "Red", Rgb24.Parse("#C91A09"), 38);

    private static PartsList Sample() =>
        new([Black6628, Grey4265C, Red32556], []);

    [Fact]
    public void Written_rows_use_the_specified_columns_in_order()
    {
        var text = PartsListCsv.Write(Sample());

        var lines = text.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

        lines[0].Should().Be("ID;Codice Lego;Codice BrickLink;Nome colore;Codice RGB;Quantita");
        lines[1].Should().Be("1;6628;11;Black;#05131D;1");
        lines[2].Should().Be("2;4265c;9;Light Gray;#9BA19D;10");
        lines[3].Should().Be("3;32556;5;Red;#C91A09;38");
    }

    [Fact]
    public void Write_then_read_returns_the_same_entries()
    {
        var read = PartsListCsv.Read(PartsListCsv.Write(Sample()));

        read.Entries.Should().Equal(Sample().Entries);
    }

    /// <summary>
    /// A file edited and re-saved by a spreadsheet can come back with a different separator,
    /// so the separator is detected rather than assumed.
    /// </summary>
    [Theory]
    [InlineData(';')]
    [InlineData(',')]
    [InlineData('\t')]
    public void The_separator_is_detected_when_reading(char delimiter)
    {
        var read = PartsListCsv.Read(PartsListCsv.Write(Sample(), delimiter));

        read.Entries.Should().Equal(Sample().Entries);
    }

    [Fact]
    public void A_byte_order_mark_does_not_confuse_the_reader()
    {
        var read = PartsListCsv.Read('﻿' + PartsListCsv.Write(Sample()));

        read.Entries.Should().Equal(Sample().Entries);
    }

    [Fact]
    public void A_file_without_a_heading_row_is_still_read()
    {
        var withoutHeader = string.Join(
            "\r\n",
            PartsListCsv.Write(Sample()).Split("\r\n", StringSplitOptions.RemoveEmptyEntries).Skip(1));

        var read = PartsListCsv.Read(withoutHeader);

        read.Entries.Should().Equal(Sample().Entries);
        read.Notes.Should().Contain(n => n.Contains("no heading row"));
    }

    [Fact]
    public void A_colour_name_containing_the_separator_survives_the_round_trip()
    {
        var awkward = Black6628 with { ColorName = "Trans-Clear; really" };

        var read = PartsListCsv.Read(PartsListCsv.Write([awkward]));

        read.Entries.Single().ColorName.Should().Be("Trans-Clear; really");
    }

    [Fact]
    public void An_empty_file_is_rejected()
    {
        var act = () => PartsListCsv.Read("");

        act.Should().Throw<FormatException>().WithMessage("*empty*");
    }

    [Fact]
    public void A_heading_row_with_no_entries_is_rejected()
    {
        var act = () => PartsListCsv.Read("ID;Codice Lego;Codice BrickLink;Nome colore;Codice RGB;Quantita\r\n");

        act.Should().Throw<FormatException>().WithMessage("*no entries*");
    }

    [Fact]
    public void A_short_row_names_the_line()
    {
        var act = () => PartsListCsv.Read("ID;Codice Lego;Codice BrickLink;Nome colore;Codice RGB;Quantita\r\n1;6628;11\r\n");

        act.Should().Throw<FormatException>().WithMessage("*Line 2*3 fields*");
    }

    [Fact]
    public void A_bad_colour_value_names_the_line()
    {
        var act = () => PartsListCsv.Read(
            "ID;Codice Lego;Codice BrickLink;Nome colore;Codice RGB;Quantita\r\n1;6628;11;Black;not-a-colour;1\r\n");

        act.Should().Throw<FormatException>().WithMessage("*Line 2*not a colour value*");
    }

    [Fact]
    public void A_zero_quantity_is_rejected()
    {
        var act = () => PartsListCsv.Read(
            "ID;Codice Lego;Codice BrickLink;Nome colore;Codice RGB;Quantita\r\n1;6628;11;Black;#05131D;0\r\n");

        act.Should().Throw<FormatException>().WithMessage("*greater than zero*");
    }

    [Fact]
    public void An_empty_part_number_is_rejected()
    {
        var act = () => PartsListCsv.Read(
            "ID;Codice Lego;Codice BrickLink;Nome colore;Codice RGB;Quantita\r\n1;;11;Black;#05131D;1\r\n");

        act.Should().Throw<FormatException>().WithMessage("*Codice Lego is empty*");
    }

    [Fact]
    public async Task Writing_a_file_adds_a_byte_order_mark_so_a_spreadsheet_reads_it_as_utf8()
    {
        var path = Path.Combine(Path.GetTempPath(), $"lego2stl-{Guid.NewGuid():N}", "parts.csv");

        try
        {
            await PartsListCsv.WriteFileAsync(path, Sample());

            var bytes = await File.ReadAllBytesAsync(path);
            bytes.Take(3).Should().Equal(0xEF, 0xBB, 0xBF);

            var reloaded = await PartsListCsv.ReadFileAsync(path);
            reloaded.Entries.Should().Equal(Sample().Entries);
        }
        finally
        {
            var dir = Path.GetDirectoryName(path);
            if (dir is not null && Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Reading_a_missing_file_says_so()
    {
        var act = () => PartsListCsv.ReadFileAsync(Path.Combine(Path.GetTempPath(), "nope-lego2stl.csv"));

        await act.Should().ThrowAsync<FileNotFoundException>();
    }
}
