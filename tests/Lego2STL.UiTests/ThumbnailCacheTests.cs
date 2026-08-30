using Avalonia.Headless.XUnit;
using FluentAssertions;
using Lego2STL.Gui.Services;

namespace Lego2STL.UiTests;

/// <summary>
/// The pictures the catalogue shows, and what it does when it cannot have one.
/// </summary>
/// <remarks>
/// Offline is the case that matters: a run made with no network must not hang the catalogue on
/// a request that will never answer, and must not report an error either - a missing picture is
/// a missing picture.
/// </remarks>
public sealed class ThumbnailCacheTests : IDisposable
{
    private readonly string _folder = Path.Combine(
        Path.GetTempPath(), "lego2stl-thumbs-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    [Fact]
    public async Task Offline_there_is_no_photo_and_no_complaint()
    {
        using var cache = new ThumbnailCache(_folder) { Offline = true };

        (await cache.TryGetPhotoAsync("32523")).Should().BeNull();
    }

    [Fact]
    public async Task A_part_number_that_is_no_part_number_gives_nothing()
    {
        using var cache = new ThumbnailCache(_folder) { Offline = true };

        (await cache.TryGetPhotoAsync("  ")).Should().BeNull();
    }

    /// <summary>A photo already on the disk is used without going near the network.</summary>
    /// <remarks>Hosted, because turning a file into a bitmap needs a drawing platform.</remarks>
    [AvaloniaFact]
    public async Task A_photo_already_fetched_is_read_from_the_disk()
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllBytes(Path.Combine(_folder, "photo-32523.png"), OnePixelPng());

        using var cache = new ThumbnailCache(_folder) { Offline = true };

        (await cache.TryGetPhotoAsync("32523")).Should().NotBeNull();
    }

    /// <summary>The smallest valid PNG: one opaque pixel.</summary>
    private static byte[] OnePixelPng() => Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");
}
