namespace Lego2STL.Core.Plates;

/// <summary>Which slicer the combined plate file is written for (#12).</summary>
public enum ProjectFormat
{
    /// <summary>A Bambu Studio project, one Bambu plate per colour plate.</summary>
    BambuStudio,

    /// <summary>An OrcaSlicer project, one Orca plate per colour plate.</summary>
    OrcaSlicer,

    /// <summary>A plain 3MF any slicer opens, with every plate side by side on one bed.</summary>
    Plain,
}

/// <summary>The formats by the names the command line takes.</summary>
public static class ProjectFormats
{
    public const ProjectFormat Default = ProjectFormat.BambuStudio;

    public static IReadOnlyList<ProjectFormat> All { get; } =
        [ProjectFormat.BambuStudio, ProjectFormat.OrcaSlicer, ProjectFormat.Plain];

    public static IReadOnlyList<string> Names { get; } = [.. All.Select(Name)];

    public static string Name(this ProjectFormat format) => format switch
    {
        ProjectFormat.OrcaSlicer => "orca",
        ProjectFormat.Plain => "3mf",
        _ => "bambu",
    };

    public static bool TryParse(string? text, out ProjectFormat format)
    {
        foreach (var candidate in All)
        {
            if (string.Equals(text?.Trim(), candidate.Name(), StringComparison.OrdinalIgnoreCase))
            {
                format = candidate;
                return true;
            }
        }

        format = Default;
        return false;
    }
}
