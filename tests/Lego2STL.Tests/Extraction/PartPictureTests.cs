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
}
