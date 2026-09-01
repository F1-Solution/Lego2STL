namespace Lego2STL.Core.Run;

/// <summary>The clearance a build will use, and the preset it came from if it came from one.</summary>
public sealed record ResolvedClearance(double Millimetres, string? FromPreset);

/// <summary>A build asked for a tolerance preset that has not been saved.</summary>
public sealed class UnknownTolerancePresetException : Exception
{
    public UnknownTolerancePresetException(string name, IReadOnlyList<string> available)
        : base($"No tolerance preset is called '{name}'.")
    {
        Name = name;
        Available = available;
    }

    public string Name { get; }

    /// <summary>The names that do exist, so the message can offer them.</summary>
    public IReadOnlyList<string> Available { get; }
}

/// <summary>
/// Decides which clearance a build uses, for both the command line and the window.
/// </summary>
/// <remarks>
/// <para>
/// Most specific first: an explicit figure, then a named preset, then the preferred preset, then
/// nothing at all. Explicit always beats remembered.
/// </para>
/// <para>
/// One function rather than one per front end. Resolving it twice is how the window and the
/// command line would come to disagree about whether a preferred preset applies, and that
/// disagreement would appear as a plate printing differently from the settings that made it.
/// </para>
/// </remarks>
public static class ClearanceChoice
{
    /// <param name="asked">The figure given explicitly, or null when none was. Zero is a figure.</param>
    public static ResolvedClearance Resolve(
        double? asked,
        string? presetName,
        IReadOnlyList<TolerancePreset> presets)
    {
        ArgumentNullException.ThrowIfNull(presets);

        if (asked is { } explicitly)
        {
            return new ResolvedClearance(explicitly, null);
        }

        if (!string.IsNullOrWhiteSpace(presetName))
        {
            return TolerancePresets.Find(presets, presetName) is { } named
                ? new ResolvedClearance(named.Millimetres, named.Name)
                : throw new UnknownTolerancePresetException(
                    presetName.Trim(),
                    [.. presets.Select(p => p.Name)]);
        }

        return presets.FirstOrDefault(p => p.Preferred) is { } preferred
            ? new ResolvedClearance(preferred.Millimetres, preferred.Name)
            : new ResolvedClearance(0.0, null);
    }
}
