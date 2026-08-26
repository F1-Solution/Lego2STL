using System.Text.Json;
using System.Text.Json.Serialization;

namespace Lego2STL.Core.Run;

/// <summary>
/// The runs that have happened, newest first.
/// </summary>
/// <remarks>
/// <para>
/// Paths and nothing else, deliberately. A cached summary beside each path would be a second
/// copy of what the folder already says, and the two would eventually disagree - a row claiming
/// a run finished while the folder it names holds a record saying it stopped. Storing only the
/// path makes "the folder is the truth" true rather than merely intended, and makes a run
/// deleted by hand simply vanish from the list.
/// </para>
/// <para>
/// The terminal and the window can both be running, so writes go through a replace and every
/// failure is swallowed: losing a history row must never cost a run. Last writer wins, which
/// costs nothing when only paths are stored.
/// </para>
/// </remarks>
public static class RunIndex
{
    private const int CurrentVersion = 1;

    private static readonly JsonSerializerOptions Format = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static string FilePath => AppDataDirectory.File("runs.json");

    /// <summary>Newest first. Empty when there is no history, or none that will read.</summary>
    public static IReadOnlyList<string> Read()
    {
        try
        {
            var path = FilePath;

            if (!File.Exists(path))
            {
                return [];
            }

            var stored = JsonSerializer.Deserialize<History>(File.ReadAllText(path), Format);

            return stored?.Runs is null ? [] : [.. stored.Runs.Where(run => !string.IsNullOrWhiteSpace(run))];
        }
        catch (Exception ex) when (ex is IOException
                                       or JsonException
                                       or UnauthorizedAccessException
                                       or NotSupportedException)
        {
            return [];
        }
    }

    /// <summary>Puts a run at the front, and leaves only one row for it.</summary>
    public static void Record(RunLayout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);

        var folder = Path.GetFullPath(layout.Root);

        Write([folder, .. Read().Where(run => !Same(run, folder))]);
    }

    public static void Forget(string folder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);

        var full = Path.GetFullPath(folder);

        Write([.. Read().Where(run => !Same(run, full))]);
    }

    public static void ForgetEverything() => Write([]);

    private static bool Same(string one, string other) =>
        string.Equals(Path.GetFullPath(one), other, StringComparison.OrdinalIgnoreCase);

    private static void Write(IReadOnlyList<string> runs)
    {
        var path = FilePath;
        var temporary = path + ".writing";

        try
        {
            Directory.CreateDirectory(AppDataDirectory.Path);

            File.WriteAllText(
                temporary, JsonSerializer.Serialize(new History(CurrentVersion, runs), Format));

            File.Move(temporary, path, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException
                                       or UnauthorizedAccessException
                                       or JsonException
                                       or NotSupportedException)
        {
            TryDelete(temporary);
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A leftover half-written file is untidy and harmless.
        }
    }

    private sealed record History(int Version, IReadOnlyList<string> Runs);
}
