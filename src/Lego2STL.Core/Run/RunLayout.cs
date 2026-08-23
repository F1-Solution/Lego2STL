namespace Lego2STL.Core.Run;

/// <summary>
/// Where a run puts everything it produces.
/// </summary>
/// <remarks>
/// One folder per run, named after the input and sitting beside it, holding the parts list,
/// the report, the shapes and the plates. Everything from a run is then in one place: easy
/// to look through, easy to archive, easy to delete, and unaffected by which directory the
/// command happened to be started from. Feeding the parts list back in later lands on the
/// same folder, so a second pass overwrites rather than scattering a second copy.
/// </remarks>
public sealed class RunLayout
{
    private RunLayout(string root, string name)
    {
        Root = root;
        Name = name;
    }

    /// <summary>The run folder.</summary>
    public string Root { get; }

    /// <summary>The input's base name, used for the folder and the parts list.</summary>
    public string Name { get; }

    public string PartsListPath => Path.Combine(Root, Name + ".csv");

    public string ReportPath => Path.Combine(Root, "report.txt");

    /// <summary>Crops of anything that could not be read, for checking by eye.</summary>
    public string ReviewDirectory => Path.Combine(Root, "review");

    public string StlDirectory => Path.Combine(Root, "stl");

    public string PlateDirectory => Path.Combine(Root, "3mf");

    /// <summary>Answers given during review, so the same question is not asked twice.</summary>
    public string OverridesPath => Path.Combine(Root, "overrides.csv");

    /// <summary>
    /// Works out the layout for an input file. When the parts list of a previous run is the
    /// input, the existing run folder is reused rather than nesting another inside it.
    /// </summary>
    public static RunLayout For(string inputPath, string? explicitOutputDirectory = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inputPath);

        var full = Path.GetFullPath(inputPath);
        var name = Path.GetFileNameWithoutExtension(full);

        if (string.IsNullOrEmpty(name))
        {
            throw new ArgumentException($"Cannot work out a name from '{inputPath}'.", nameof(inputPath));
        }

        if (explicitOutputDirectory is not null)
        {
            return new RunLayout(Path.Combine(Path.GetFullPath(explicitOutputDirectory), name), name);
        }

        var directory = Path.GetDirectoryName(full)
            ?? throw new ArgumentException($"'{inputPath}' has no containing folder.", nameof(inputPath));

        // Re-running from a previous run's parts list: stay in that folder.
        if (string.Equals(Path.GetFileName(directory), name, StringComparison.OrdinalIgnoreCase))
        {
            return new RunLayout(directory, name);
        }

        return new RunLayout(Path.Combine(directory, name), name);
    }

    public void CreateDirectories()
    {
        Directory.CreateDirectory(Root);
    }

    /// <summary>Relative to the run folder, for readable messages.</summary>
    public string Describe(string path) =>
        path.StartsWith(Root, StringComparison.OrdinalIgnoreCase)
            ? Path.GetRelativePath(Root, path)
            : path;
}
