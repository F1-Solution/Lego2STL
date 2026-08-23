using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Lego2STL.Core.Colors;

namespace Lego2STL.Gui.Services;

/// <summary>
/// Fetches the little picture of a part in its colour, and keeps it.
/// </summary>
/// <remarks>
/// <para>
/// The pictures are renders of the same shape library the geometry comes from, in the same
/// colours, so a catalogue of them looks like the pieces in the box rather than like a
/// spreadsheet. That is worth a good deal when checking that what was read off the pages is
/// really what is on them.
/// </para>
/// <para>
/// Entirely optional, and quiet when it fails. There is no picture for every part in every
/// colour, the machine may be offline, and none of that should interrupt anything: a part with
/// no picture shows its colour as a plain swatch and is no less usable. Each one is kept on
/// disk after the first fetch, so a second look costs nothing.
/// </para>
/// </remarks>
public sealed class ThumbnailCache : IDisposable
{
    private const string UrlPattern = "https://cdn.rebrickable.com/media/parts/ldraw/{0}/{1}.png";

    private readonly HttpClient _http;
    private readonly string _directory;
    private readonly SemaphoreSlim _atOnce = new(4, 4);

    public ThumbnailCache(string? directory = null)
    {
        _directory = directory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Lego2STL",
            "thumbnails");

        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("Lego2STL/1.0");
    }

    /// <summary>Whether to go to the network at all. Off mirrors the run's own offline setting.</summary>
    public bool Offline { get; set; }

    /// <summary>
    /// The picture of a part in a colour, or null when there is not one to be had.
    /// </summary>
    public async Task<Bitmap?> TryGetAsync(
        string partNumber,
        LegoColor color,
        CancellationToken cancellationToken = default)
    {
        if (color.LDrawId is not { } ldrawColor || string.IsNullOrWhiteSpace(partNumber))
        {
            return null;
        }

        var path = Path.Combine(_directory, $"{ldrawColor}-{Safe(partNumber)}.png");

        try
        {
            if (File.Exists(path))
            {
                return Load(path);
            }

            if (Offline)
            {
                return null;
            }

            await _atOnce.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var url = string.Format(
                    System.Globalization.CultureInfo.InvariantCulture, UrlPattern, ldrawColor, partNumber);

                using var response = await _http.GetAsync(url, cancellationToken).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }

                Directory.CreateDirectory(_directory);
                var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
                await File.WriteAllBytesAsync(path, bytes, cancellationToken).ConfigureAwait(false);

                return Load(path);
            }
            finally
            {
                _atOnce.Release();
            }
        }
        catch (Exception ex) when (ex is HttpRequestException
                                       or IOException
                                       or TaskCanceledException
                                       or UnauthorizedAccessException
                                       or ArgumentException)
        {
            return null;
        }
    }

    private static Bitmap? Load(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            return new Bitmap(stream);
        }
        catch (Exception ex) when (ex is IOException or ArgumentException)
        {
            return null;
        }
    }

    private static string Safe(string partNumber) =>
        string.Concat(partNumber.Split(Path.GetInvalidFileNameChars()));

    public void Dispose()
    {
        _http.Dispose();
        _atOnce.Dispose();
    }
}
