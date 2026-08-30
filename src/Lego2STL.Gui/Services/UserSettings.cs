using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Lego2STL.Core.Run;
using Lego2STL.Core.Text;

namespace Lego2STL.Gui.Services;

/// <summary>
/// The few choices worth remembering between one use of the window and the next.
/// </summary>
/// <remarks>
/// Deliberately short. Remembering the language and where the shape library lives saves
/// setting them up every time; remembering which part was last selected, or how the window
/// was scrolled, would be clutter that eventually goes wrong. A file that cannot be read is
/// treated as no file, because losing a preference is a much smaller problem than refusing
/// to start.
/// </remarks>
public sealed class UserSettings
{
    [JsonPropertyName("language")]
    public string? Language { get; set; }

    [JsonPropertyName("ldrawDirectory")]
    public string? LDrawDirectory { get; set; }

    /// <summary>
    /// Kept between uses like the shape library, and for the same reason: it is a folder on
    /// this machine that will be the same folder next time.
    /// </summary>
    [JsonPropertyName("elementMap")]
    public string? ElementMap { get; set; }

    [JsonPropertyName("outputDirectory")]
    public string? OutputDirectory { get; set; }

    [JsonPropertyName("printer")]
    public string? Printer { get; set; }

    /// <summary>Which numbering the catalogue last showed.</summary>
    [JsonPropertyName("partNumbering")]
    public string? PartNumbering { get; set; }

    /// <summary>Where parts can be bought, in the order they are offered.</summary>
    [JsonPropertyName("shops")]
    public List<Shop> Shops { get; set; } = [];

    /// <summary>The name of the shop whose button the catalogue shows.</summary>
    [JsonPropertyName("preferredShop")]
    public string? PreferredShop { get; set; }

    [JsonIgnore]
    public DisplayLanguage DisplayLanguage =>
        DisplayLanguages.TryParse(Language, out var parsed)
            ? parsed
            : DisplayLanguages.FromEnvironment();

    /// <summary>
    /// Where the preferences live. The variable lets a test, or a copy carried on a stick,
    /// keep its own preferences instead of writing into the account they happen to run under.
    /// </summary>
    public const string DirectoryVariable = AppDataDirectory.Variable;

    public static string FilePath => AppDataDirectory.File("interface.json");

    private static readonly JsonSerializerOptions Format = new() { WriteIndented = true };

    public static UserSettings Load()
    {
        try
        {
            return File.Exists(FilePath)
                ? JsonSerializer.Deserialize<UserSettings>(File.ReadAllText(FilePath)) ?? new UserSettings()
                : new UserSettings();
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return new UserSettings();
        }
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, Format));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Not being able to remember a preference is not worth interrupting anyone over.
        }
    }
}
