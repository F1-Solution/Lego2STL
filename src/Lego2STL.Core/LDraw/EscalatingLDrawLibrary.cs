using Lego2STL.Core.Text;

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

    /// <summary>Whether the escalation may end in the whole 144 MB library; false on a phone.</summary>
    public bool AllowFullArchive { get; init; } = true;

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
    private readonly Strings _words;
    private readonly List<ILDrawLibrary> _owned = [];

    private DirectoryLDrawLibrary? _local;
    private HttpLDrawLibrary? _perFile;
    private ZipLDrawLibrary? _complete;
    private bool _perFileAbandoned;

    public EscalatingLDrawLibrary(
        LDrawSourceOptions? options = null,
        Action<string>? log = null,
        Strings? words = null)
    {
        _options = options ?? new LDrawSourceOptions();
        _log = log ?? (_ => { });
        _words = words ?? Strings.English;

        _local = DirectoryLDrawLibrary.TryOpen(_options.LocalDirectory, _words);
        if (_local is not null)
        {
            _log(_words.Format(TextKey.MsgLDrawUsingFolder, _local.Description));
        }
        else if (_options.LocalDirectory is not null)
        {
            _log(_words.Format(TextKey.MsgLDrawNotALibrary, _options.LocalDirectory));
        }

        // A previously downloaded archive counts as local: no reason to fetch anything.
        var archivePath = CompleteArchivePath();
        if (_local is null && _options.AllowFullArchive && File.Exists(archivePath))
        {
            _complete = ZipLDrawLibrary.Open(archivePath, _words);
            _owned.Add(_complete);
            _log(_words.Format(TextKey.MsgLDrawUsingDownloaded, _complete.EntryCount));
        }
    }

    public string Description =>
        _complete?.Description ?? _local?.Description ?? _perFile?.Description
        ?? _words[TextKey.LibraryNone];

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
                _log(_words.Format(TextKey.MsgLDrawRefused, _perFile.RefusalCount));

                if (_options.AllowFullArchive)
                {
                    _perFileAbandoned = true;
                    await DownloadCompleteAsync(cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    _log(_words[TextKey.MsgLDrawArchiveSkipped]);
                }
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

        if (_complete is null && _options.AllowFullArchive)
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
            _options.IncludeUnofficial,
            words: _words);

        _owned.Add(library);
        _log(_words[TextKey.MsgLDrawFetchingPerFile]);
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
            _log(_words.Format(TextKey.MsgLDrawDownloading, _options.CompleteArchiveUrl));

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

        _complete = ZipLDrawLibrary.Open(path, _words);
        _owned.Add(_complete);
        _log(_words.Format(TextKey.MsgLDrawReady, _complete.EntryCount, path));
    }

    public void Dispose()
    {
        foreach (var owned in _owned.OfType<IDisposable>())
        {
            owned.Dispose();
        }
    }
}
