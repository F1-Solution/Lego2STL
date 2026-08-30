using Lego2STL.Core.Pipeline;
using Lego2STL.Core.Rebrickable;

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

    /// <summary>What the run recorded about itself, written from the moment it starts.</summary>
    public string ManifestPath => Path.Combine(Root, "run.json");

    /// <summary>Everything the run said, kept beside what it produced.</summary>
    public string LogPath => Path.Combine(Root, "run.log");

    /// <summary>Crops of anything that could not be read, for checking by eye.</summary>
    public string ReviewDirectory => Path.Combine(Root, "review");

    public string StlDirectory => Path.Combine(Root, "stl");

    /// <summary>A picture of each part, cut from the document it was read from.</summary>
    public string ImageDirectory => Path.Combine(Root, "images");

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

    /// <summary>The layout of a folder that already exists, named after itself.</summary>
    public static RunLayout At(string folder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);

        var full = Path.GetFullPath(folder);
        var name = Path.GetFileName(full);

        return string.IsNullOrEmpty(name)
            ? throw new ArgumentException($"Cannot work out a name from '{folder}'.", nameof(folder))
            : new RunLayout(full, name);
    }

    /// <summary>
    /// The folder a run will use, worked out before it starts.
    /// </summary>
    /// <remarks>
    /// The same function the pipeline itself calls, which is what makes the folder the window
    /// names - and the log file it offers inside it - the folder the run really writes to.
    /// Null when the input is not yet enough to name one, which is the normal state of a window
    /// being filled in.
    /// </remarks>
    public static RunLayout? Plan(RunSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        try
        {
            if (settings.Kind == InputKind.SetNumber)
            {
                if (string.IsNullOrWhiteSpace(settings.SetNumber))
                {
                    return null;
                }

                var name = SetFolderName(settings.SetNumber);
                var root = settings.OutputDirectory ?? Environment.CurrentDirectory;
                return For(Path.Combine(root, name + ".csv"), null);
            }

            return string.IsNullOrWhiteSpace(settings.InputPath)
                ? null
                : For(settings.InputPath, settings.OutputDirectory);
        }
        catch (Exception ex) when (ex is ArgumentException
                                       or NotSupportedException
                                       or PathTooLongException)
        {
            return null;
        }
    }

    /// <summary>Where a set's run folder goes, given its number.</summary>
    public static string SetFolderName(string setNumber) =>
        "set-" + RebrickableClient.NormaliseSetNumber(setNumber);

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
