namespace Lego2STL.Core.LDraw;

/// <summary>How the library should be obtained.</summary>
public sealed record LDrawSourceOptions
{
    /// <summary>An existing library folder to use in preference to anything else.</summary>
    public string? LocalDirectory { get; init; }

    /// <summary>Where fetched files and the downloaded archive are kept between runs.</summary>
    public string? CacheDirectory { get; init; }

    /// <summary>Never use the network. A missing file is then simply missing.</summary>
    public bool Offline { get; init; }

    /// <summary>Also look in the unofficial part collection.</summary>
    public bool IncludeUnofficial { get; init; } = true;

    /// <summary>
    /// How many refused requests to tolerate before giving up on fetching file by file and
    /// taking the whole library instead.
    /// </summary>
    public int RefusalsBeforeFullDownload { get; init; } = 3;

    /// <summary>Where the whole library is downloaded from when it comes to that.</summary>
    public string CompleteArchiveUrl { get; init; } = "https://library.ldraw.org/library/updates/complete.zip";

    public string ResolvedCacheDirectory =>
        CacheDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Lego2STL",
            "ldraw");
}

/// <summary>
/// Gets LDraw files from whichever source can, moving to a heavier one only when needed.
/// </summary>
/// <remarks>
/// <para>
/// Three sources, in increasing cost. An existing library folder is free and complete. Fetching
/// file by file needs no big download and suits a handful of parts, but the server limits how
/// fast it will answer and one part can need dozens of files. Taking the whole library is a
/// single large download that then makes everything free and offline forever.
/// </para>
/// <para>
/// Rather than choose up front, this starts cheap and escalates: once the server has refused
/// enough requests, continuing to ask file by file is slower than downloading everything, so it
/// switches. Which source answered is recorded, so the report can say how the run was served.
/// </para>
/// </remarks>
public sealed class EscalatingLDrawLibrary : ILDrawLibrary, IDisposable
{
    private readonly LDrawSourceOptions _options;
    private readonly Action<string> _log;
    private readonly List<ILDrawLibrary> _owned = [];

    private DirectoryLDrawLibrary? _local;
    private HttpLDrawLibrary? _perFile;
    private ZipLDrawLibrary? _complete;
    private bool _perFileAbandoned;

    public EscalatingLDrawLibrary(LDrawSourceOptions? options = null, Action<string>? log = null)
    {
        _options = options ?? new LDrawSourceOptions();
        _log = log ?? (_ => { });

        _local = DirectoryLDrawLibrary.TryOpen(_options.LocalDirectory);
        if (_local is not null)
        {
            _log($"Using the LDraw library already on disk: {_local.Description}.");
        }
        else if (_options.LocalDirectory is not null)
        {
            _log($"'{_options.LocalDirectory}' does not look like an LDraw library (no 'parts' or 'p' folder); ignoring it.");
        }

        // A previously downloaded archive counts as local: no reason to fetch anything.
        var archivePath = CompleteArchivePath();
        if (_local is null && File.Exists(archivePath))
        {
            _complete = ZipLDrawLibrary.Open(archivePath);
            _owned.Add(_complete);
            _log($"Using the LDraw library downloaded earlier ({_complete.EntryCount} files).");
        }
    }

    public string Description =>
        _complete?.Description ?? _local?.Description ?? _perFile?.Description ?? "no LDraw source";

    /// <summary>Files that no source could supply.</summary>
    public IReadOnlyCollection<string> Missing => _missing;

    private readonly SortedSet<string> _missing = new(StringComparer.Ordinal);

    public async Task<string?> TryReadAsync(string reference, CancellationToken cancellationToken = default)
    {
        if (_local is not null)
        {
            var fromLocal = await _local.TryReadAsync(reference, cancellationToken).ConfigureAwait(false);
            if (fromLocal is not null)
            {
                return fromLocal;
            }
        }

        if (_complete is not null)
        {
            var fromArchive = await _complete.TryReadAsync(reference, cancellationToken).ConfigureAwait(false);
            if (fromArchive is not null)
            {
                return fromArchive;
            }

            _missing.Add(LDrawReference.Normalise(reference));
            return null;
        }

        if (_options.Offline)
        {
            _missing.Add(LDrawReference.Normalise(reference));
            return null;
        }

        if (!_perFileAbandoned)
        {
            _perFile ??= CreatePerFile();

            var fetched = await _perFile.TryReadAsync(reference, cancellationToken).ConfigureAwait(false);

            if (_perFile.RefusalCount >= _options.RefusalsBeforeFullDownload)
            {
                _log($"The library website refused {_perFile.RefusalCount} requests; " +
                     "downloading the whole library instead, which is faster from here on.");
                _perFileAbandoned = true;
                await DownloadCompleteAsync(cancellationToken).ConfigureAwait(false);
            }

            if (fetched is not null)
            {
                return fetched;
            }

            if (!_perFileAbandoned)
            {
                _missing.Add(LDrawReference.Normalise(reference));
                return null;
            }
        }

        if (_complete is null)
        {
            await DownloadCompleteAsync(cancellationToken).ConfigureAwait(false);
        }

        if (_complete is null)
        {
            _missing.Add(LDrawReference.Normalise(reference));
            return null;
        }

        var last = await _complete.TryReadAsync(reference, cancellationToken).ConfigureAwait(false);
        if (last is null)
        {
            _missing.Add(LDrawReference.Normalise(reference));
        }

        return last;
    }

    private HttpLDrawLibrary CreatePerFile()
    {
        var library = new HttpLDrawLibrary(
            Path.Combine(_options.ResolvedCacheDirectory, "files"),
            _options.IncludeUnofficial);

        _owned.Add(library);
        _log("Fetching LDraw files from the library website as they are needed.");
        return library;
    }

    private string CompleteArchivePath() =>
        Path.Combine(_options.ResolvedCacheDirectory, "complete.zip");

    private async Task DownloadCompleteAsync(CancellationToken cancellationToken)
    {
        if (_options.Offline)
        {
            return;
        }

        var path = CompleteArchivePath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        if (!File.Exists(path))
        {
            _log($"Downloading the LDraw library from {_options.CompleteArchiveUrl} (about 145 MB, once).");

            using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (compatible; Lego2STL/1.0)");

            var temporary = path + ".part";

            using (var response = await http.GetAsync(
                       _options.CompleteArchiveUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                       .ConfigureAwait(false))
            {
                response.EnsureSuccessStatusCode();

                await using var target = File.Create(temporary);
                await response.Content.CopyToAsync(target, cancellationToken).ConfigureAwait(false);
            }

            File.Move(temporary, path, overwrite: true);
        }

        _complete = ZipLDrawLibrary.Open(path);
        _owned.Add(_complete);
        _log($"LDraw library ready: {_complete.EntryCount} files, cached at {path}.");
    }

    public void Dispose()
    {
        foreach (var owned in _owned.OfType<IDisposable>())
        {
            owned.Dispose();
        }
    }
}
