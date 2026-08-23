using FluentAssertions;
using Lego2STL.Core.Colors;

namespace Lego2STL.Tests.Colors;

public sealed class ColorTableCsvTests
{
    private static readonly LegoColor Black =
        new(0, "Black", Rgb24.Parse("#05131D"), false, 11, 26, 0, 840746);

    private static readonly LegoColor LightBluishGray =
        new(71, "Light Bluish Gray", Rgb24.Parse("#A0A5A9"), false, 86, 194, 71, 519221);

    private static readonly LegoColor TransClear =
        new(47, "Trans-Clear", Rgb24.Parse("#FCFCFC"), true, 12, 21, 47, 100);

    private static readonly ColorCodeMapping[] Mappings =
    [
        new(ColorScheme.Rebrickable, 0, 0),
        new(ColorScheme.Rebrickable, 71, 71),
        new(ColorScheme.Rebrickable, 47, 47),
        new(ColorScheme.BrickLink, 11, 0),
        new(ColorScheme.BrickLink, 86, 71),
        new(ColorScheme.Lego, 26, 0),
        new(ColorScheme.Lego, 342, 0),
        new(ColorScheme.LDraw, 0, 0),
    ];

    [Fact]
    public void Write_then_read_preserves_every_colour_and_mapping()
    {
        var written = ColorTableCsv.Write([Black, LightBluishGray, TransClear], Mappings);

        var table = ColorTableCsv.Read(written);

        table.Count.Should().Be(3);
        table.Get(ColorScheme.BrickLink, 11).Should().Be(Black);
        table.Get(ColorScheme.BrickLink, 86).Should().Be(LightBluishGray);
        table.Get(ColorScheme.Lego, 342).Should().Be(Black, "alias codes survive the round trip");
        table.Get(ColorScheme.Rebrickable, 47).IsTranslucent.Should().BeTrue();
    }

    [Fact]
    public void Write_is_stable_so_a_regeneration_produces_a_reviewable_diff()
    {
        var first = ColorTableCsv.Write([LightBluishGray, Black], Mappings);
        // Enumerable.Reverse explicitly: on an array, Reverse() otherwise binds to
        // Span<T>.Reverse(), which reverses in place and returns void.
        var second = ColorTableCsv.Write([Black, LightBluishGray], Enumerable.Reverse(Mappings).ToArray());

        second.Should().Be(first, "output is ordered, not input-ordered");
    }

    [Fact]
    public void Comments_and_blank_lines_are_ignored()
    {
        var content = ColorTableCsv.Write([Black], [new ColorCodeMapping(ColorScheme.BrickLink, 11, 0)]);

        var withNoise = content.Replace("C;0;Black", "# a comment\n\nC;0;Black");

        ColorTableCsv.Read(withNoise).Get(ColorScheme.BrickLink, 11).Name.Should().Be("Black");
    }

    [Fact]
    public void A_colour_line_with_the_wrong_field_count_names_the_line()
    {
        var act = () => ColorTableCsv.Read("C;rebrickable_id;x\nC;0;Black;#05131D\n");

        act.Should().Throw<FormatException>().WithMessage("*line 2*4 fields*");
    }

    [Fact]
    public void An_unknown_record_type_names_the_line()
    {
        var act = () => ColorTableCsv.Read("C;rebrickable_id;x\nX;0;whatever\n");

        act.Should().Throw<FormatException>().WithMessage("*line 2*expected 'C' or 'M'*");
    }

    [Fact]
    public void An_unknown_scheme_in_a_mapping_names_the_line()
    {
        var act = () => ColorTableCsv.Read("C;rebrickable_id;x\nC;0;Black;#05131D;0;11;26;0;1\nM;Peeron;5;0\n");

        act.Should().Throw<FormatException>().WithMessage("*line 3*not a known colour scheme*");
    }

    [Fact]
    public void A_mapping_pointing_at_a_missing_colour_is_rejected()
    {
        var act = () => ColorTableCsv.Read(
            "C;rebrickable_id;x\nC;0;Black;#05131D;0;11;26;0;1\nM;BrickLink;11;999\n");

        act.Should().Throw<InvalidOperationException>().WithMessage("*not in the table*");
    }

    [Fact]
    public void Two_mappings_for_the_same_code_are_rejected()
    {
        var act = () => ColorTableCsv.Read(
            "C;rebrickable_id;x\n" +
            "C;0;Black;#05131D;0;11;26;0;1\n" +
            "C;71;Light Bluish Gray;#A0A5A9;0;86;194;71;1\n" +
            "M;BrickLink;11;0\nM;BrickLink;11;71\n");

        act.Should().Throw<InvalidOperationException>().WithMessage("*two entries for BrickLink 11*");
    }

    [Fact]
    public void An_empty_reference_is_rejected()
    {
        var act = () => ColorTableCsv.Read("# only a comment\n");

        act.Should().Throw<FormatException>().WithMessage("*no colours*");
    }

    [Fact]
    public void A_colour_name_containing_the_delimiter_is_refused_at_write_time()
    {
        var bad = Black with { Name = "Semi;colon" };

        var act = () => ColorTableCsv.Write([bad], []);

        act.Should().Throw<InvalidOperationException>().WithMessage("*delimiter*");
    }
}
