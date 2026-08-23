using System.Numerics;
using FluentAssertions;
using Lego2STL.Core.Plates;

namespace Lego2STL.Tests.Plates;

public sealed class ShelfPackerTests
{
    private static readonly PrintBed SmallBed = new("test", 100f, 100f, 100f);

    private static PackingOptions Options(float spacing = 2f, float margin = 5f) =>
        new() { Bed = SmallBed, Spacing = spacing, Margin = margin };

    private static PackableItem Item(string name, float w, float d, float h = 5f) =>
        new(name, new Vector2(w, d), h);

    [Fact]
    public void An_empty_list_produces_no_plates()
    {
        var result = ShelfPacker.Pack([], Options());

        result.Plates.Should().BeEmpty();
        result.Oversized.Should().BeEmpty();
    }

    [Fact]
    public void Everything_that_fits_is_placed()
    {
        var items = Enumerable.Range(0, 12).Select(i => Item($"p{i}", 20f, 20f)).ToList();

        var result = ShelfPacker.Pack(items, Options());

        result.Oversized.Should().BeEmpty();
        result.Plates.Sum(p => p.PieceCount).Should().Be(12);
    }

    [Fact]
    public void Nothing_overlaps_and_nothing_leaves_the_bed()
    {
        var sizes = new[] { 30f, 12f, 25f, 8f, 19f, 40f, 6f, 22f, 15f, 11f };
        var items = sizes.Select((s, i) => Item($"p{i}", s, s * 0.7f)).ToList();

        var options = Options();
        var result = ShelfPacker.Pack(items, options);

        foreach (var plate in result.Plates)
        {
            foreach (var placed in plate.Items)
            {
                placed.X.Should().BeGreaterThanOrEqualTo(options.Margin);
                placed.Y.Should().BeGreaterThanOrEqualTo(options.Margin);
                (placed.X + placed.Item.Footprint.X)
                    .Should().BeLessThanOrEqualTo(SmallBed.Width - options.Margin + 0.001f);
                (placed.Y + placed.Item.Footprint.Y)
                    .Should().BeLessThanOrEqualTo(SmallBed.Depth - options.Margin + 0.001f);
            }

            foreach (var (a, b) in Pairs(plate.Items))
            {
                Overlaps(a, b).Should().BeFalse(
                    "{0} at ({1},{2}) must not sit on top of {3} at ({4},{5})",
                    a.Item.PartNumber, a.X, a.Y, b.Item.PartNumber, b.X, b.Y);
            }
        }
    }

    [Fact]
    public void A_part_wider_than_the_bed_is_reported_rather_than_squeezed_on()
    {
        var result = ShelfPacker.Pack([Item("huge", 500f, 10f), Item("fine", 10f, 10f)], Options());

        result.Oversized.Should().ContainSingle()
            .Which.Item.PartNumber.Should().Be("huge");
        result.Plates.Sum(p => p.PieceCount).Should().Be(1);
    }

    [Fact]
    public void A_part_taller_than_the_machine_is_reported_as_too_tall()
    {
        var result = ShelfPacker.Pack([Item("tower", 10f, 10f, h: 500f)], Options());

        var only = result.Oversized.Should().ContainSingle().Subject;
        only.TooTall.Should().BeTrue();
    }

    [Fact]
    public void More_than_one_plate_is_used_when_one_is_not_enough()
    {
        var items = Enumerable.Range(0, 40).Select(i => Item($"p{i}", 40f, 40f)).ToList();

        var result = ShelfPacker.Pack(items, Options());

        result.Plates.Count.Should().BeGreaterThan(1);
        result.Plates.Sum(p => p.PieceCount).Should().Be(40);
    }

    /// <summary>
    /// Two runs of the same list have to lay out the same way, or two plate files for the same
    /// input cannot be compared with each other.
    /// </summary>
    [Fact]
    public void The_same_list_lays_out_the_same_way_every_time()
    {
        var items = Enumerable.Range(0, 25).Select(i => Item($"p{i}", 10f + (i % 7), 8f + (i % 5))).ToList();

        var first = ShelfPacker.Pack(items, Options());
        var second = ShelfPacker.Pack(Enumerable.Reverse(items), Options());

        Flatten(first).Should().Equal(Flatten(second));
    }

    [Fact]
    public void Plates_are_numbered_from_one()
    {
        var items = Enumerable.Range(0, 40).Select(i => Item($"p{i}", 40f, 40f)).ToList();

        var result = ShelfPacker.Pack(items, Options());

        result.Plates.Select(p => p.Number).Should().Equal(
            Enumerable.Range(1, result.Plates.Count));
    }

    private static IEnumerable<string> Flatten(PackingResult result) =>
        result.Plates.SelectMany(p => p.Items.Select(i =>
            $"{p.Number}:{i.Item.PartNumber}@{i.X:0.###},{i.Y:0.###}"));

    private static IEnumerable<(PlacedItem A, PlacedItem B)> Pairs(IReadOnlyList<PlacedItem> items)
    {
        for (var i = 0; i < items.Count; i++)
        {
            for (var j = i + 1; j < items.Count; j++)
            {
                yield return (items[i], items[j]);
            }
        }
    }

    private static bool Overlaps(PlacedItem a, PlacedItem b) =>
        a.X < b.X + b.Item.Footprint.X &&
        b.X < a.X + a.Item.Footprint.X &&
        a.Y < b.Y + b.Item.Footprint.Y &&
        b.Y < a.Y + a.Item.Footprint.Y;
}
