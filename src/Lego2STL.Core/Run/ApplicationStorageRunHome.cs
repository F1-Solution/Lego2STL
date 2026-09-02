using Lego2STL.Core.Pipeline;

namespace Lego2STL.Core.Run;

/// <summary>
/// One folder under application storage, for a platform that has nowhere else to write.
/// </summary>
/// <remarks>
/// The picked document is copied into the same root before the run starts, so "under
/// application storage" and "beside the input" name the same folder and a run's contents
/// look identical to a desktop run's.
/// </remarks>
public sealed class ApplicationStorageRunHome : IRunHome
{
    public ApplicationStorageRunHome(string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        Root = Path.GetFullPath(root);
    }

    /// <summary>Where every run this application makes is written.</summary>
    public string Root { get; }

    public RunLayout? Plan(RunSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        // An explicit output directory is a person's instruction and outranks the default.
        var root = settings.OutputDirectory ?? Root;

        if (settings.Kind == InputKind.SetNumber)
        {
            return string.IsNullOrWhiteSpace(settings.SetNumber)
                ? null
                : RunLayout.At(Path.Combine(root, RunLayout.SetFolderName(settings.SetNumber)));
        }

        return string.IsNullOrWhiteSpace(settings.InputPath)
            ? null
            : RunLayout.For(settings.InputPath, root);
    }
}
