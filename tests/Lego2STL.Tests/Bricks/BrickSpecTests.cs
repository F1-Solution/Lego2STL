using FluentAssertions;
using Lego2STL.Core.Bricks;

namespace Lego2STL.Tests.Bricks;

public sealed class BrickSpecTests
{
    [Theory]
    [InlineData("2x4", 2, 4)]
    [InlineData("1x1", 1, 1)]
    [InlineData("16x2", 16, 2)]
    [InlineData(" 2 x 4 ", 2, 4)]
    [InlineData("2X4", 2, 4)]
    [InlineData("2*4", 2, 4)]
    public void A_size_is_read_however_it_is_written(string text, int columns, int rows)
    {
        var spec = BrickSpec.Parse(text);

        spec.Columns.Should().Be(columns);
        spec.Rows.Should().Be(rows);
    }

    /// <summary>
    /// A brick is three plates tall, and a plate and a tile are one. That is the convention
    /// these pieces are described by, so it is the default rather than something to state.
    /// </summary>
    [Theory]
    [InlineData(BrickKind.Brick, 3)]
    [InlineData(BrickKind.Plate, 1)]
    [InlineData(BrickKind.Tile, 1)]
    public void Each_kind_has_its_own_height(BrickKind kind, int expected)
    {
        BrickSpec.Parse("2x4", kind).PlateCount.Should().Be(expected);
    }

    [Fact]
    public void A_third_number_is_the_height_in_plates()
    {
        BrickSpec.Parse("2x4x6").PlateCount.Should().Be(6);
        BrickSpec.Parse("2x4x1", BrickKind.Brick).PlateCount.Should().Be(1);
    }

    /// <summary>A tile is a plate with nothing on top, so asking for knobs contradicts it.</summary>
    [Fact]
    public void A_tile_has_no_knobs_however_it_is_asked_for()
    {
        BrickSpec.Parse("2x4", BrickKind.Tile, knobs: true).Knobs.Should().BeFalse();
    }

    [Fact]
    public void Knobs_can_be_left_off_a_brick()
    {
        BrickSpec.Parse("2x4", BrickKind.Brick, knobs: false).Knobs.Should().BeFalse();
    }

    [Fact]
    public void An_empty_size_is_refused()
    {
        var act = () => BrickSpec.Parse("");

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("brick")]
    [InlineData("2")]
    [InlineData("2x")]
    [InlineData("x4")]
    [InlineData("2x4x")]
    [InlineData("2x4x5x6")]
    [InlineData("-2x4")]
    [InlineData("2.5x4")]
    public void Something_that_is_not_a_size_is_refused(string text)
    {
        var act = () => BrickSpec.Parse(text);

        act.Should().Throw<FormatException>();
    }

    [Theory]
    [InlineData("0x4")]
    [InlineData("2x0")]
    [InlineData("100x4")]
    [InlineData("2x4x0")]
    [InlineData("2x4x100")]
    public void A_size_no_real_piece_has_is_refused(string text)
    {
        var act = () => BrickSpec.Parse(text);

        act.Should().Throw<FormatException>();
    }

    /// <summary>
    /// The file name has to say what the piece is. A folder of generated shapes is useless if
    /// telling them apart means opening them.
    /// </summary>
    [Fact]
    public void The_file_name_says_what_the_piece_is()
    {
        BrickSpec.Parse("2x4").FileName.Should().Be("brick-2x4x3");
        BrickSpec.Parse("2x4", BrickKind.Plate).FileName.Should().Be("plate-2x4x1");
        BrickSpec.Parse("2x4", BrickKind.Tile).FileName.Should().Be("tile-2x4x1");
        BrickSpec.Parse("2x4x6").FileName.Should().Be("brick-2x4x6");
    }

    [Fact]
    public void The_file_name_says_when_a_piece_is_unusual()
    {
        BrickSpec.Parse("2x4", knobs: false).FileName.Should().Be("brick-2x4x3-smooth");
        BrickSpec.Parse("2x4", studHoles: false).FileName.Should().Be("brick-2x4x3-solid");
    }

    [Fact]
    public void Two_different_pieces_never_share_a_file_name()
    {
        var names = new[]
        {
            BrickSpec.Parse("2x4"),
            BrickSpec.Parse("4x2"),
            BrickSpec.Parse("2x4", BrickKind.Plate),
            BrickSpec.Parse("2x4", BrickKind.Tile),
            BrickSpec.Parse("2x4x6"),
            BrickSpec.Parse("2x4", knobs: false),
            BrickSpec.Parse("2x4", studHoles: false),
        }.Select(s => s.FileName).ToList();

        names.Should().OnlyHaveUniqueItems();
    }
}
