using Lego2STL.Core.Catalogue;

namespace Lego2STL.Core.Run;

/// <summary>
/// A run folder as it stands on the disk now.
/// </summary>
/// <remarks>
/// <para>
/// The folder is the truth: the history is a list of paths, and everything shown about a run is
/// read back from the folder that path names. That is what makes a run deleted by hand simply
/// disappear, and a run copied onto another machine open there.
/// </para>
/// <para>
/// Nothing here throws. A folder that has been deleted, a record from a build that does not
/// exist yet, a parts list truncated by a full disk - each is a state this reports, because the
/// alternative is a history list that cannot be scrolled past its first bad row.
/// </para>
/// </remarks>
public sealed record RunFolder(
    RunLayout Layout,
    RunManifest? Manifest,
    ManifestState State,
    PartsList? PartsList,
    bool HasPartsList,
    bool HasShapes,
    bool HasPlates,
    bool HasReport,
    bool HasLog)
{
    /// <summary>Whether the folder is still there at all.</summary>
    public bool Exists { get; init; }

    /// <summary>Reads a folder back. Synchronous; callers run it off the interface thread.</summary>
    public static RunFolder Read(string folder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);

        var layout = RunLayout.At(folder);

        if (!Directory.Exists(layout.Root))
        {
            return new RunFolder(layout, null, ManifestState.Missing, null, false, false, false, false, false);
        }

        var (manifest, state) = RunManifest.Read(layout.ManifestPath);
        var hasPartsList = File.Exists(layout.PartsListPath);

        return new RunFolder(
            layout,
            manifest,
            state,
            hasPartsList ? ReadPartsList(layout.PartsListPath) : null,
            hasPartsList,
            HasFilesIn(layout.StlDirectory),
            HasFilesIn(layout.PlateDirectory),
            File.Exists(layout.ReportPath),
            File.Exists(layout.LogPath))
        {
            Exists = true,
        };
    }

    /// <summary>The document this folder amounts to, record or no record.</summary>
    public RunDocument ToDocument() =>
        State == ManifestState.Missing
            ? RunDocument.WithoutManifest(Layout, PartsList)
            : RunDocument.From(Manifest!, Layout) with { FromNewerBuild = State == ManifestState.Newer };

    /// <remarks>
    /// A list that will not read leaves the list null while the folder still says the file is
    /// there, which is honest: something was written, and this build cannot make sense of it.
    /// </remarks>
    private static PartsList? ReadPartsList(string path)
    {
        try
        {
            return PartsListCsv.Read(File.ReadAllText(path));
        }
        catch (Exception ex) when (ex is IOException
                                       or InvalidDataException
                                       or FormatException
                                       or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static bool HasFilesIn(string directory)
    {
        try
        {
            return Directory.Exists(directory) && Directory.EnumerateFiles(directory).Any();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
