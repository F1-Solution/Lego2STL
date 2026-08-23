using System.Text.Json;

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

    public static string ConfigFilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Lego2STL",
        "config.json");

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

    private static string? ReadFromConfigFile()
    {
        var path = ConfigFilePath;
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            if (doc.RootElement.TryGetProperty("rebrickableApiKey", out var value) &&
                value.ValueKind == JsonValueKind.String)
            {
                var key = value.GetString();
                return string.IsNullOrWhiteSpace(key) ? null : key.Trim();
            }
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"{path} is not valid JSON: {ex.Message}", ex);
        }
        catch (IOException ex)
        {
            throw new InvalidOperationException($"Could not read {path}: {ex.Message}", ex);
        }

        return null;
    }
}
