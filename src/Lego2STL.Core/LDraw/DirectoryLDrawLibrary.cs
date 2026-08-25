using Lego2STL.Core.Text;

namespace Lego2STL.Core.LDraw;

/// <summary>
/// Reads LDraw files from a folder on disk, such as an existing library installation.
/// </summary>
public sealed class DirectoryLDrawLibrary : ILDrawLibrary
{
    private readonly string _root;
    private readonly Dictionary<string, string?> _cache = new(StringComparer.Ordinal);

    private DirectoryLDrawLibrary(string root, Strings words)
    {
        _root = root;
        Description = words.Format(TextKey.LibraryFolder, root);
    }

    public string Description { get; }

    /// <summary>
    /// Opens a folder as a library, or returns null when it does not look like one.
    /// </summary>
    /// <remarks>
    /// The check is for a "parts" or "p" subfolder, because a folder without them cannot
    /// resolve anything and silently returning nothing for every lookup would be worse than
    /// saying so up front.
    /// </remarks>
    public static DirectoryLDrawLibrary? TryOpen(string? directory, Strings? words = null)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            return null;
        }

        var root = Path.GetFullPath(directory);

        // Some installations put the library in an "ldraw" subfolder.
        foreach (var candidate in new[] { root, Path.Combine(root, "ldraw") })
        {
            if (Directory.Exists(Path.Combine(candidate, "parts")) ||
                Directory.Exists(Path.Combine(candidate, "p")))
            {
                return new DirectoryLDrawLibrary(candidate, words ?? Strings.English);
            }
        }

        return null;
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
            var path = Path.Combine(_root, relative.Replace('/', Path.DirectorySeparatorChar));

            if (File.Exists(path))
            {
                var text = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
                _cache[key] = text;
                return text;
            }
        }

        _cache[key] = null;
        return null;
    }
}
