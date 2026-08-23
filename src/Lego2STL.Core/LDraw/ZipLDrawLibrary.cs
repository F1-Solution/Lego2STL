using System.IO.Compression;
using System.Text;

namespace Lego2STL.Core.LDraw;

/// <summary>
/// Reads LDraw files straight out of the library's distribution archive.
/// </summary>
/// <remarks>
/// <para>
/// The archive holds around 37,000 entries and the whole thing is read into memory once, so
/// that no lookup touches the disk or the virus scanner afterwards. That costs about 140 MB
/// and buys a large speedup: reopening the archive per lookup is hundreds of times slower
/// than keeping one open.
/// </para>
/// <para>
/// An index of entry names is built once on opening, because entries are stored as full paths
/// while files refer to each other by bare name. Both the full path and the bare name are
/// indexed, with the bare-name index resolving in library search order.
/// </para>
/// </remarks>
public sealed class ZipLDrawLibrary : ILDrawLibrary, IDisposable
{
    private readonly ZipArchive _archive;
    private readonly MemoryStream _stream;
    private readonly Dictionary<string, ZipArchiveEntry> _byPath;
    private readonly Dictionary<string, string?> _cache = new(StringComparer.Ordinal);
    private readonly string _description;

    private ZipLDrawLibrary(
        MemoryStream stream,
        ZipArchive archive,
        Dictionary<string, ZipArchiveEntry> byPath,
        string description)
    {
        _stream = stream;
        _archive = archive;
        _byPath = byPath;
        _description = description;
    }

    public string Description => _description;

    public int EntryCount => _byPath.Count;

    public static ZipLDrawLibrary Open(string archivePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(archivePath);

        if (!File.Exists(archivePath))
        {
            throw new FileNotFoundException($"No such archive: {archivePath}", archivePath);
        }

        return Open(File.ReadAllBytes(archivePath), $"archive {Path.GetFileName(archivePath)}");
    }

    public static ZipLDrawLibrary Open(byte[] archiveBytes, string description)
    {
        ArgumentNullException.ThrowIfNull(archiveBytes);

        var stream = new MemoryStream(archiveBytes, writable: false);

        try
        {
            var archive = new ZipArchive(stream, ZipArchiveMode.Read);
            return new ZipLDrawLibrary(stream, archive, BuildIndex(archive), description);
        }
        catch (InvalidDataException ex)
        {
            stream.Dispose();
            throw new InvalidDataException($"{description} is not a readable zip archive: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Indexes entries by their library-relative path, stripping the archive's own top-level
    /// folder so that "ldraw/parts/3001.dat" is found as "parts/3001.dat".
    /// </summary>
    private static Dictionary<string, ZipArchiveEntry> BuildIndex(ZipArchive archive)
    {
        var index = new Dictionary<string, ZipArchiveEntry>(archive.Entries.Count, StringComparer.Ordinal);

        foreach (var entry in archive.Entries)
        {
            if (entry.FullName.EndsWith('/'))
            {
                continue;
            }

            var path = entry.FullName.Replace('\\', '/').ToLowerInvariant();

            if (path.StartsWith("ldraw/", StringComparison.Ordinal))
            {
                path = path["ldraw/".Length..];
            }

            index.TryAdd(path, entry);
        }

        return index;
    }

    public async Task<string?> TryReadAsync(string reference, CancellationToken cancellationToken = default)
    {
        var key = LDrawReference.Normalise(reference);

        if (_cache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        foreach (var relative in LDrawReference.CandidatePaths(reference))
        {
            if (!_byPath.TryGetValue(relative, out var entry))
            {
                continue;
            }

            using var entryStream = entry.Open();
            using var reader = new StreamReader(entryStream, Encoding.UTF8);
            var text = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);

            _cache[key] = text;
            return text;
        }

        _cache[key] = null;
        return null;
    }

    public void Dispose()
    {
        _archive.Dispose();
        _stream.Dispose();
    }
}
