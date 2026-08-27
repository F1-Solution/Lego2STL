using Lego2STL.Core.Run;

namespace Lego2STL.Tests.Run;

/// <summary>
/// A settings folder of its own for the length of one test.
/// </summary>
/// <remarks>
/// The folder is chosen by one process-wide environment variable, so tests that point it
/// somewhere have to take turns: every such class carries <c>[Collection(AppDataFolder.Name)]</c>,
/// which is what stops one class reading the history another class was in the middle of
/// replacing. The old value is put back on the way out, so a suite run leaves the account's own
/// history alone.
/// </remarks>
internal sealed class AppDataFolder : IDisposable
{
    public const string Name = "app data folder";

    private readonly string? previous;

    public AppDataFolder()
    {
        previous = Environment.GetEnvironmentVariable(AppDataDirectory.Variable);
        Path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "lego2stl-appdata-" + Guid.NewGuid().ToString("N"));

        Environment.SetEnvironmentVariable(AppDataDirectory.Variable, Path);
    }

    public string Path { get; }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(AppDataDirectory.Variable, previous);

        try
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A temporary folder left behind is the operating system's problem, not a failure.
        }
    }
}

/// <summary>Declares the collection those classes name, so they run one at a time.</summary>
[CollectionDefinition(AppDataFolder.Name)]
public sealed class AppDataFolderCollection;
