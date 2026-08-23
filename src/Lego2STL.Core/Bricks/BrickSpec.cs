using System.Globalization;
using System.Text.RegularExpressions;

namespace Lego2STL.Core.Bricks;

/// <summary>What kind of piece to generate.</summary>
public enum BrickKind
{
    /// <summary>Full height, three plates tall.</summary>
    Brick,

    /// <summary>One plate tall.</summary>
    Plate,

    /// <summary>One plate tall with a smooth top.</summary>
    Tile,
}

/// <summary>
/// A piece asked for by size, e.g. "2x4" or "1x8x1".
/// </summary>
/// <param name="Columns">Studs across.</param>
/// <param name="Rows">Studs deep.</param>
/// <param name="Kind">Which of the three shapes.</param>
/// <param name="Plates">
/// Height in plates. A brick is three, a plate and a tile are one; naming a size in the spec
/// overrides that, so "2x4x6" is a brick twice normal height.
/// </param>
/// <param name="Knobs">Studs on top. Off for a tile.</param>
/// <param name="StudHoles">Hollow underside tubes, which is what makes a piece grip.</param>
public sealed partial record BrickSpec(
    int Columns,
    int Rows,
    BrickKind Kind = BrickKind.Brick,
    int? Plates = null,
    bool Knobs = true,
    bool StudHoles = true)
{
    /// <summary>Height in plates, taking the kind's own height when none was named.</summary>
    public int PlateCount => Plates ?? Kind switch
    {
        BrickKind.Brick => 3,
        _ => 1,
    };

    /// <summary>A name for the file, saying what it is without needing the command back.</summary>
    public string FileName
    {
        get
        {
            var kind = Kind.ToString().ToLowerInvariant();
            var name = string.Create(
                CultureInfo.InvariantCulture, $"{kind}-{Columns}x{Rows}x{PlateCount}");

            if (!Knobs && Kind != BrickKind.Tile)
            {
                name += "-smooth";
            }

            if (!StudHoles)
            {
                name += "-solid";
            }

            return name;
        }
    }

    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"{Columns}x{Rows}x{PlateCount} {Kind}");

    /// <summary>
    /// Reads a size such as "2x4" or "2x4x6". The third number, when given, is the height in
    /// plates rather than in studs, because that is how these pieces are actually described:
    /// a brick is three plates tall and everything else is counted against that.
    /// </summary>
    public static BrickSpec Parse(
        string text,
        BrickKind kind = BrickKind.Brick,
        bool? knobs = null,
        bool studHoles = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        var match = SizePattern().Match(text.Trim());

        if (!match.Success)
        {
            throw new FormatException(
                $"'{text}' is not a size. One looks like 2x4, or 2x4x6 to say how many plates tall.");
        }

        var columns = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
        var rows = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);

        int? plates = match.Groups[3].Success
            ? int.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture)
            : null;

        foreach (var (value, what) in new[] { (columns, "width"), (rows, "depth") })
        {
            if (value is < 1 or > 64)
            {
                throw new FormatException($"A {what} of {value} is not a real piece; use 1 to 64.");
            }
        }

        if (plates is < 1 or > 64)
        {
            throw new FormatException($"A height of {plates} plates is not a real piece; use 1 to 64.");
        }

        // A tile is a plate with nothing on top; asking for knobs on one is a contradiction,
        // so the kind wins and the flag is ignored rather than producing something that is
        // neither.
        var wantsKnobs = kind != BrickKind.Tile && (knobs ?? true);

        return new BrickSpec(columns, rows, kind, plates, wantsKnobs, studHoles);
    }

    [GeneratedRegex(@"^(\d+)\s*[xX*]\s*(\d+)(?:\s*[xX*]\s*(\d+))?$")]
    private static partial Regex SizePattern();
}
