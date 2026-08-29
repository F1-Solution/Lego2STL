using System.Globalization;

namespace Lego2STL.Core.Plates;

/// <summary>
/// How much room a printer has, in millimetres.
/// </summary>
/// <param name="Name">What to call it in messages and in the report.</param>
/// <param name="Width">Left to right.</param>
/// <param name="Depth">Front to back.</param>
/// <param name="Height">Floor to the top of the travel.</param>
public sealed record PrintBed(string Name, float Width, float Depth, float Height)
{
    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"{Width:0.#} x {Depth:0.#} x {Height:0.#} mm");

    /// <summary>
    /// The bed written the way <c>--plate-size</c> takes it back.
    /// </summary>
    /// <remarks>
    /// The height is left off when it matches the width, because that is the shape those two
    /// numbers already mean - saying it again would be a third number that carries nothing.
    /// Kept here so the one way of spelling a bed size is written once.
    /// </remarks>
    public string AsSize => Math.Abs(Height - Width) < 0.05f
        ? string.Create(CultureInfo.InvariantCulture, $"{Width:0.#}x{Depth:0.#}")
        : string.Create(CultureInfo.InvariantCulture, $"{Width:0.#}x{Depth:0.#}x{Height:0.#}");

    /// <summary>True when a part of this size could be printed at all, laid out as it stands.</summary>
    public bool Fits(float width, float depth, float height) =>
        width <= Width && depth <= Depth && height <= Height;
}

/// <summary>
/// The printers the tool knows, and how to name one that it does not.
/// </summary>
/// <remarks>
/// A named printer is a convenience, not a limit: <see cref="Parse"/> also accepts a plain
/// size, so a machine that is not on this list is a matter of typing its bed rather than
/// waiting for the list to grow.
/// </remarks>
public static class PrintBeds
{
    public static PrintBed A1 { get; } = new("A1", 256f, 256f, 256f);

    public static PrintBed A1Mini { get; } = new("A1 mini", 180f, 180f, 180f);

    public static PrintBed P1P { get; } = new("P1P", 256f, 256f, 256f);

    public static PrintBed P1S { get; } = new("P1S", 256f, 256f, 256f);

    public static PrintBed X1C { get; } = new("X1C", 256f, 256f, 256f);

    public static PrintBed H2D { get; } = new("H2D", 350f, 320f, 325f);

    /// <summary>
    /// Every printer by name. A1 is first and is the default: it is the most common of these
    /// beds, and four of the six share its size, so a layout packed for it suits most of the
    /// list unchanged.
    /// </summary>
    public static IReadOnlyList<PrintBed> All { get; } = [A1, A1Mini, P1P, P1S, X1C, H2D];

    public static PrintBed Default => A1;

    /// <summary>Names as they are written on the command line.</summary>
    public static IReadOnlyList<string> Names { get; } =
        [.. All.Select(b => b.Name.Replace(" ", string.Empty, StringComparison.Ordinal))];

    public static bool TryGetByName(string? name, out PrintBed bed)
    {
        bed = Default;

        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        var wanted = name.Replace(" ", string.Empty, StringComparison.Ordinal).Trim();

        foreach (var candidate in All)
        {
            var candidateName = candidate.Name.Replace(" ", string.Empty, StringComparison.Ordinal);
            if (string.Equals(candidateName, wanted, StringComparison.OrdinalIgnoreCase))
            {
                bed = candidate;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Reads either a printer's name or a plain size such as "220x220" or "300x300x400".
    /// Two numbers leave the height at the width, which is the usual shape of these machines.
    /// </summary>
    public static PrintBed Parse(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        if (TryGetByName(text, out var named))
        {
            return named;
        }

        if (TryParseSize(text, out var sized))
        {
            return sized;
        }

        throw new FormatException(
            $"'{text}' is neither a printer this knows nor a bed size. " +
            $"Printers: {string.Join(", ", Names)}. A size looks like 220x220 or 300x300x400.");
    }

    public static bool TryParseSize(string? text, out PrintBed bed)
    {
        bed = Default;

        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var parts = text.Split(['x', 'X', '*'], StringSplitOptions.TrimEntries);

        if (parts.Length is < 2 or > 3)
        {
            return false;
        }

        var numbers = new float[parts.Length];
        for (var i = 0; i < parts.Length; i++)
        {
            if (!float.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out numbers[i])
                || numbers[i] <= 0f)
            {
                return false;
            }
        }

        var width = numbers[0];
        var depth = numbers[1];
        var height = parts.Length == 3 ? numbers[2] : width;

        bed = new PrintBed(
            string.Create(CultureInfo.InvariantCulture, $"{width:0.#}x{depth:0.#}"),
            width,
            depth,
            height);

        return true;
    }
}
