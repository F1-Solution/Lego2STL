using System.Reflection;
using System.Text;

namespace Lego2STL.Core.Colors;

/// <summary>
/// Access to the colour cross-reference that ships inside the tool.
/// </summary>
/// <remarks>
/// Loaded from an embedded resource so a normal run needs no API key, no network and no
/// HTML scraping. Regenerate it with <c>lego2stl refresh-colors</c>.
/// </remarks>
public static class ColorReference
{
    /// <summary>File name under <c>src/Lego2STL.Core/Data/</c>, and the resource suffix.</summary>
    public const string FileName = "colors.reference.csv";

    private static readonly Lazy<ColorTable> Cached = new(Load, LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>The shipped table. Parsed once per process.</summary>
    public static ColorTable Table => Cached.Value;

    private static ColorTable Load()
    {
        var assembly = typeof(ColorReference).Assembly;
        var name = FindResourceName(assembly);

        using var stream = assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"Embedded resource '{name}' could not be opened.");
        using var reader = new StreamReader(stream, Encoding.UTF8);

        return ColorTableCsv.Read(reader.ReadToEnd());
    }

    private static string FindResourceName(Assembly assembly)
    {
        var match = assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith(FileName, StringComparison.OrdinalIgnoreCase));

        return match ?? throw new InvalidOperationException(
            $"The colour cross-reference is missing from this build: no embedded resource ends with '{FileName}'. " +
            $"Generate it with 'lego2stl refresh-colors --out src/Lego2STL.Core/Data/{FileName}' and rebuild.");
    }
}
