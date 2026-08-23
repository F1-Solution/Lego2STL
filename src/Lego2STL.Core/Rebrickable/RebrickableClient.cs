using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Lego2STL.Core.Rebrickable;

/// <summary>
/// Minimal Rebrickable API v3 client: the four things this tool needs, nothing more.
/// </summary>
/// <remarks>
/// Deliberately hand-rolled rather than taking a client library. RebrickableSharp exists
/// (C#, MIT) but was last touched in July 2024, and these are four plain GETs; a
/// dependency would add more surface than it removes.
/// </remarks>
public sealed class RebrickableClient : IDisposable
{
    private const string BaseUrl = "https://rebrickable.com/api/v3/lego/";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly HttpClient _http;
    private readonly bool _ownsHttp;
    private readonly SemaphoreSlim _throttle = new(1, 1);
    private DateTimeOffset _nextAllowedCall = DateTimeOffset.MinValue;

    /// <summary>Free-tier keys are rate limited; one request per second is comfortably inside it.</summary>
    private static readonly TimeSpan MinimumInterval = TimeSpan.FromMilliseconds(1000);

    public RebrickableClient(string apiKey, HttpClient? http = null)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new ArgumentException("An API key is required.", nameof(apiKey));
        }

        _ownsHttp = http is null;
        _http = http ?? new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("key", apiKey);
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("Lego2STL/1.0");
    }

    /// <summary>All 275 colours, with their BrickLink / LEGO / LDraw cross-references.</summary>
    internal async Task<IReadOnlyList<RbColor>> GetColorsAsync(CancellationToken ct = default) =>
        await GetAllPagesAsync<RbColor>("colors/?page_size=1000", ct).ConfigureAwait(false);

    /// <summary>
    /// The inventory of a set, e.g. "42100-1". Rebrickable requires the "-1" variant
    /// suffix; it is added when missing.
    /// </summary>
    internal async Task<IReadOnlyList<RbSetPart>> GetSetPartsAsync(string setNumber, CancellationToken ct = default)
    {
        var normalised = NormaliseSetNumber(setNumber);
        return await GetAllPagesAsync<RbSetPart>(
            $"sets/{Uri.EscapeDataString(normalised)}/parts/?page_size=1000", ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Resolves a BrickLink part number to Rebrickable parts. Necessary because the two
    /// numbering systems differ: BrickLink's "4265c" is not a Rebrickable part number at
    /// all, and maps to two Rebrickable parts (32123a and 32123b).
    /// </summary>
    internal async Task<IReadOnlyList<RbPart>> FindPartsByBrickLinkIdAsync(string brickLinkId, CancellationToken ct = default) =>
        await GetAllPagesAsync<RbPart>(
            $"parts/?bricklink_id={Uri.EscapeDataString(brickLinkId)}&page_size=100", ct).ConfigureAwait(false);

    /// <summary>A single part by its Rebrickable number, or null when there is no such part.</summary>
    internal async Task<RbPart?> GetPartAsync(string partNumber, CancellationToken ct = default) =>
        await GetOrNullAsync<RbPart>($"parts/{Uri.EscapeDataString(partNumber)}/", ct).ConfigureAwait(false);

    /// <summary>
    /// Adds Rebrickable's variant suffix when the caller gave a bare set number:
    /// "42100" becomes "42100-1".
    /// </summary>
    public static string NormaliseSetNumber(string setNumber)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(setNumber);
        var s = setNumber.Trim();
        return s.Contains('-') ? s : s + "-1";
    }

    private async Task<IReadOnlyList<T>> GetAllPagesAsync<T>(string relativeUrl, CancellationToken ct)
    {
        var all = new List<T>();
        var url = BaseUrl + relativeUrl;

        while (!string.IsNullOrEmpty(url))
        {
            var page = await SendAsync<RbPage<T>>(url, allowNotFound: false, ct).ConfigureAwait(false)
                       ?? throw new RebrickableException($"Empty response from {Redact(url)}.");
            all.AddRange(page.Results);
            url = page.Next ?? "";
        }

        return all;
    }

    private async Task<T?> GetOrNullAsync<T>(string relativeUrl, CancellationToken ct) where T : class =>
        await SendAsync<T>(BaseUrl + relativeUrl, allowNotFound: true, ct).ConfigureAwait(false);

    private async Task<T?> SendAsync<T>(string url, bool allowNotFound, CancellationToken ct) where T : class
    {
        const int maxAttempts = 4;

        for (var attempt = 1; ; attempt++)
        {
            await WaitForSlotAsync(ct).ConfigureAwait(false);

            using var response = await _http.GetAsync(url, ct).ConfigureAwait(false);

            if (response.StatusCode == HttpStatusCode.NotFound && allowNotFound)
            {
                return null;
            }

            if (response.StatusCode == HttpStatusCode.TooManyRequests && attempt < maxAttempts)
            {
                var wait = response.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(attempt * 2);
                await Task.Delay(wait, ct).ConfigureAwait(false);
                continue;
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new RebrickableException(
                    $"Rebrickable returned {(int)response.StatusCode} {response.ReasonPhrase} for {Redact(url)}." +
                    (response.StatusCode == HttpStatusCode.Unauthorized
                        ? " The API key appears to be invalid."
                        : ""));
            }

            var json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            try
            {
                return JsonSerializer.Deserialize<T>(json, JsonOptions);
            }
            catch (JsonException ex)
            {
                throw new RebrickableException($"Could not parse the response from {Redact(url)}: {ex.Message}", ex);
            }
        }
    }

    /// <summary>Spaces requests out so a long run cannot trip the rate limiter.</summary>
    private async Task WaitForSlotAsync(CancellationToken ct)
    {
        await _throttle.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var now = DateTimeOffset.UtcNow;
            if (now < _nextAllowedCall)
            {
                await Task.Delay(_nextAllowedCall - now, ct).ConfigureAwait(false);
            }

            _nextAllowedCall = DateTimeOffset.UtcNow + MinimumInterval;
        }
        finally
        {
            _throttle.Release();
        }
    }

    /// <summary>The key travels in a header, but URLs are still kept out of messages verbatim.</summary>
    private static string Redact(string url) => url.Replace(BaseUrl, "/lego/", StringComparison.Ordinal);

    public void Dispose()
    {
        _throttle.Dispose();
        if (_ownsHttp)
        {
            _http.Dispose();
        }
    }
}

public sealed class RebrickableException : Exception
{
    public RebrickableException(string message) : base(message) { }

    public RebrickableException(string message, Exception inner) : base(message, inner) { }
}
