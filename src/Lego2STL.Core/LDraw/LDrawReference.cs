namespace Lego2STL.Core.LDraw;

/// <summary>
/// Works out where a referenced LDraw file could live, and in what order to look.
/// </summary>
/// <remarks>
/// References are written inconsistently across the library - sometimes bare, sometimes with
/// a folder, with either slash, in any case - so every lookup has to try a sequence of
/// candidate paths rather than one. The order matters: parts before primitives, because a
/// name existing in both should resolve to the part.
/// </remarks>
public static class LDrawReference
{
    /// <summary>Normalises a reference to forward slashes and lower case.</summary>
    public static string Normalise(string reference)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reference);

        return reference.Trim().Replace('\\', '/').ToLowerInvariant();
    }

    /// <summary>
    /// The library-relative paths to try, in order.
    /// </summary>
    public static IReadOnlyList<string> CandidatePaths(string reference)
    {
        var name = Normalise(reference);

        // Already carries its folder.
        if (name.StartsWith("s/", StringComparison.Ordinal))
        {
            return [$"parts/{name}", $"p/{name}"];
        }

        if (name.StartsWith("48/", StringComparison.Ordinal) ||
            name.StartsWith("8/", StringComparison.Ordinal))
        {
            return [$"p/{name}"];
        }

        if (name.StartsWith("parts/", StringComparison.Ordinal) ||
            name.StartsWith("p/", StringComparison.Ordinal) ||
            name.StartsWith("models/", StringComparison.Ordinal))
        {
            return [name];
        }

        // A bare name: parts first, then primitives, then the sub-part and resolution folders.
        return
        [
            $"parts/{name}",
            $"p/{name}",
            $"parts/s/{name}",
            $"p/48/{name}",
            $"p/8/{name}",
        ];
    }

    /// <summary>
    /// True when a part file is only a redirection to another part rather than geometry.
    /// </summary>
    /// <remarks>
    /// The library keeps retired numbers alive as stubs whose whole body is a single reference
    /// to the replacement - "4265c" is one, standing in for "32123". The converter follows
    /// these without noticing, but the redirection is worth reporting, because the shape a
    /// user receives for the number they asked for is really another part's.
    /// </remarks>
    public static string? TryReadMovedTo(string content)
    {
        ArgumentNullException.ThrowIfNull(content);

        foreach (var line in content.Split('\n').Take(4))
        {
            var trimmed = line.Trim();

            // "0 ~Moved to 32123"
            const string marker = "0 ~Moved to ";
            if (trimmed.StartsWith(marker, StringComparison.OrdinalIgnoreCase))
            {
                var target = trimmed[marker.Length..].Trim();
                return target.Length == 0 ? null : target;
            }
        }

        return null;
    }
}
