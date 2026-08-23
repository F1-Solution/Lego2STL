namespace Lego2STL.Core.Extraction;

/// <summary>An axis-aligned pixel rectangle, inclusive of all four edges.</summary>
public readonly record struct PixelBounds(int Left, int Top, int Right, int Bottom)
{
    public int Width => Right - Left + 1;

    public int Height => Bottom - Top + 1;

    public PixelBounds Union(PixelBounds other) => new(
        Math.Min(Left, other.Left),
        Math.Min(Top, other.Top),
        Math.Max(Right, other.Right),
        Math.Max(Bottom, other.Bottom));

    /// <summary>Grows the rectangle by <paramref name="margin"/>, clamped to the given size.</summary>
    public PixelBounds Inflate(int margin, int width, int height) => new(
        Math.Max(0, Left - margin),
        Math.Max(0, Top - margin),
        Math.Min(width - 1, Right + margin),
        Math.Min(height - 1, Bottom + margin));

    public static PixelBounds Around(IEnumerable<PixelBounds> boxes)
    {
        ArgumentNullException.ThrowIfNull(boxes);

        PixelBounds? result = null;
        foreach (var box in boxes)
        {
            result = result is null ? box : result.Value.Union(box);
        }

        return result ?? throw new ArgumentException("No boxes to bound.", nameof(boxes));
    }

    public override string ToString() => $"({Left},{Top})-({Right},{Bottom})";
}
