using System.Text.Json;
using Lego2STL.Core.Run;

namespace Lego2STL.Core.Rebrickable;

/// <summary>
/// Finds the Rebrickable API key without ever putting it in the repository.
/// </summary>
/// <remarks>
/// Resolution order, first hit wins:
/// <list type="number">
///   <item>an explicit value, e.g. from <c>--api-key</c></item>
///   <item>the <c>REBRICKABLE_API_KEY</c> environment variable</item>
///   <item><c>%APPDATA%\Lego2STL\config.json</c>, as <c>{ "rebrickableApiKey": "..." }</c></item>
/// </list>
/// A normal run needs no key at all: the colour cross-reference is vendored, and LDraw
/// geometry comes from library.ldraw.org. A key is only needed for <c>--set</c>,
/// <c>--refresh-colors</c> and fetching catalogue thumbnails.
/// </remarks>
public static class RebrickableApiKey
{
    public const string EnvironmentVariable = "REBRICKABLE_API_KEY";

    /// <summary>
    /// The key's one home on this machine, beside the history and the window's preferences.
    /// </summary>
    /// <remarks>
    /// Through <see cref="AppDataDirectory"/> rather than working the path out again here, so a
    /// test, or a copy carried on a stick, keeps its own key along with the rest of its state
    /// instead of reading one out of the account it happened to be run under.
    /// </remarks>
    public static string ConfigFilePath => AppDataDirectory.File("config.json");

    /// <summary>Returns the key, or null when none is configured.</summary>
    public static string? Find(string? explicitKey = null)
    {
        if (!string.IsNullOrWhiteSpace(explicitKey))
        {
            return explicitKey.Trim();
        }

        var fromEnv = Environment.GetEnvironmentVariable(EnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(fromEnv))
        {
            return fromEnv.Trim();
        }

        return ReadFromConfigFile();
    }

    /// <summary>Returns the key, or throws with instructions on how to supply one.</summary>
    public static string Require(string? explicitKey = null) =>
        Find(explicitKey) ?? throw new InvalidOperationException(
            "No Rebrickable API key. Supply one with --api-key, or set the " +
            $"{EnvironmentVariable} environment variable, or create {ConfigFilePath} " +
            "containing {\"rebrickableApiKey\": \"...\"}. " +
            "Get a free key at https://rebrickable.com/api/");

    /// <summary>
    /// Remembers the key, so it is typed once rather than once per start.
    /// </summary>
    /// <remarks>
    /// Into the file the command line already reads, so a key given to the window is a key the
    /// terminal has too. A blank clears it. Every other setting in the file is kept: this owns
    /// one property, not the file. Not being able to write is reported, because a key that
    /// silently failed to save is exactly the disappearance this exists to end.
    /// </remarks>
    public static void Save(string? key)
    {
        var path = ConfigFilePath;
        var settings = ReadConfigFile(path) ?? [];

        if (string.IsNullOrWhiteSpace(key))
        {
            settings.Remove("rebrickableApiKey");
        }
        else
        {
            settings["rebrickableApiKey"] = JsonSerializer.SerializeToElement(key.Trim());
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(settings, Indented));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new InvalidOperationException($"Could not write {path}: {ex.Message}", ex);
        }
    }

    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    private static string? ReadFromConfigFile()
    {
        if (ReadConfigFile(ConfigFilePath) is not { } settings ||
            !settings.TryGetValue("rebrickableApiKey", out var value) ||
            value.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var key = value.GetString();
        return string.IsNullOrWhiteSpace(key) ? null : key.Trim();
    }

    /// <summary>Everything the file holds, or null when there is no file.</summary>
    private static Dictionary<string, JsonElement>? ReadConfigFile(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
                File.ReadAllText(path));
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"{path} is not valid JSON: {ex.Message}", ex);
        }
        catch (IOException ex)
        {
            throw new InvalidOperationException($"Could not read {path}: {ex.Message}", ex);
        }
    }
}
