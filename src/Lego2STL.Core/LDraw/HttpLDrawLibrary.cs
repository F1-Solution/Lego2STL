using System.Net;

namespace Lego2STL.Core.LDraw;

/// <summary>
/// Fetches individual LDraw files from the library website, caching them on disk.
/// </summary>
/// <remarks>
/// <para>
/// Cheap when only a few parts are needed and wasteful when many are: a single part can pull
/// in dozens of files, and the server rate-limits. Measured against the reference set: one
/// panel needed 37 files, and about sixty rapid requests were enough to start being refused.
/// The request without a browser-style user agent is refused outright.
/// </para>
/// <para>
/// Requests are therefore spaced out, refusals are retried with a growing wait, and
/// everything fetched is written to a cache folder so a second run needs no network at all.
/// <see cref="RefusalCount"/> is what tells the caller it is time to stop asking file by file
/// and take the whole library instead.
/// </para>
/// </remarks>
public sealed class HttpLDrawLibrary : ILDrawLibrary, IDisposable
{
    private const string OfficialBase = "https://library.ldraw.org/library/official/";
    private const string UnofficialBase = "https://library.ldraw.org/library/unofficial/";

    private readonly HttpClient _http;
    private readonly bool _ownsHttp;
    private readonly string _cacheDirectory;
    private readonly bool _includeUnofficial;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Dictionary<string, string?> _memory = new(StringComparer.Ordinal);
    private DateTimeOffset _nextRequest = DateTimeOffset.MinValue;

    /// <summary>Spacing between requests, to stay inside the server's limit.</summary>
    private static readonly TimeSpan MinimumInterval = TimeSpan.FromMilliseconds(120);

    public HttpLDrawLibrary(
        string cacheDirectory,
        bool includeUnofficial = true,
        HttpClient? http = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cacheDirectory);

        _cacheDirectory = Path.GetFullPath(cacheDirectory);
        _includeUnofficial = includeUnofficial;
        _ownsHttp = http is null;
        _http = http ?? new HttpClient { Timeout = TimeSpan.FromSeconds(30) };

        if (!_http.DefaultRequestHeaders.Contains("User-Agent"))
        {
            // Requests without a browser-style agent are refused by the server.
            _http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (compatible; Lego2STL/1.0)");
        }

        Directory.CreateDirectory(_cacheDirectory);
    }

    public string Description => "the LDraw library website, one file at a time";

    /// <summary>How many requests the server refused for rate reasons.</summary>
    public int RefusalCount { get; private set; }

    /// <summary>How many files were actually fetched over the network.</summary>
    public int FetchCount { get; private set; }

    public async Task<string?> TryReadAsync(string reference, CancellationToken cancellationToken = default)
    {
        var key = LDrawReference.Normalise(reference);

        if (_memory.TryGetValue(key, out var remembered))
        {
            return remembered;
        }

        var cachePath = CachePathFor(key);
        if (File.Exists(cachePath))
        {
            var cached = await File.ReadAllTextAsync(cachePath, cancellationToken).ConfigureAwait(false);
            var result = cached.Length == 0 ? null : cached;   // empty file records a known miss
            _memory[key] = result;
            return result;
        }

        foreach (var relative in LDrawReference.CandidatePaths(reference))
        {
            foreach (var baseUrl in Bases())
            {
                var text = await GetAsync(baseUrl + relative, cancellationToken).ConfigureAwait(false);
                if (text is null)
                {
                    continue;
                }

                await File.WriteAllTextAsync(cachePath, text, cancellationToken).ConfigureAwait(false);
                _memory[key] = text;
                return text;
            }
        }

        // Record the miss so a rerun does not repeat the whole search.
        await File.WriteAllTextAsync(cachePath, "", cancellationToken).ConfigureAwait(false);
        _memory[key] = null;
        return null;
    }

    private IEnumerable<string> Bases()
    {
        yield return OfficialBase;

        if (_includeUnofficial)
        {
            yield return UnofficialBase;
        }
    }

    private async Task<string?> GetAsync(string url, CancellationToken cancellationToken)
    {
        const int maxAttempts = 3;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            await WaitForSlotAsync(cancellationToken).ConfigureAwait(false);

            using var response = await _http.GetAsync(url, cancellationToken).ConfigureAwait(false);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                RefusalCount++;

                if (attempt == maxAttempts)
                {
                    return null;
                }

                var wait = response.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(attempt);
                await Task.Delay(wait, cancellationToken).ConfigureAwait(false);
                continue;
            }

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            FetchCount++;
            return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        }

        return null;
    }

    private async Task WaitForSlotAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var now = DateTimeOffset.UtcNow;
            if (now < _nextRequest)
            {
                await Task.Delay(_nextRequest - now, cancellationToken).ConfigureAwait(false);
            }

            _nextRequest = DateTimeOffset.UtcNow + MinimumInterval;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Flattens a reference into a single safe file name inside the cache folder.</summary>
    private string CachePathFor(string normalisedReference) =>
        Path.Combine(_cacheDirectory, normalisedReference.Replace('/', '~'));

    public void Dispose()
    {
        _gate.Dispose();

        if (_ownsHttp)
        {
            _http.Dispose();
        }
    }
}
