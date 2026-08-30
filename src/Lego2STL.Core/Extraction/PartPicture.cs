using Lego2STL.Core.Ocr;
using SkiaSharp;

namespace Lego2STL.Core.Extraction;

/// <summary>
/// Cuts a part's drawing out of the page its label was read from.
/// </summary>
/// <remarks>
/// A catalogue prints the drawing above the label, and the run only ever located the label, so
/// the band is taken by rule. It is generous on purpose: a band too tall shows some white, while
/// a band too short cuts the part in half.
/// </remarks>
public static class PartPicture
{
    /// <summary>How many label heights of page to take above the label.</summary>
    public const int BandInLabelHeights = 3;

    /// <summary>How much of the label's width to add on each side, as a fraction.</summary>
    public const double SpreadEachSide = 0.5;

    /// <summary>The region a part's drawing occupies, above its label.</summary>
    /// <param name="ceiling">
    /// The highest row the band may use, which is the row under whatever the page prints above
    /// this entry. Zero for the top of the page.
    /// </param>
    public static PixelBounds BandAbove(
        PixelBounds label, int pageWidth, int pageHeight, int ceiling = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pageWidth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pageHeight);

        var spread = (int)(label.Width * SpreadEachSide);

        var left = Math.Max(0, label.Left - spread);
        var right = Math.Min(pageWidth - 1, label.Right + spread);
        var bottom = Math.Max(0, label.Top - 1);
        var top = Math.Max(
            Math.Max(0, ceiling), label.Top - (BandInLabelHeights * Math.Max(1, label.Height)));

        return new PixelBounds(left, Math.Min(top, bottom), right, Math.Max(top, bottom));
    }

    /// <summary>
    /// The row under the nearest entry printed above this one, or zero when there is none.
    /// </summary>
    /// <remarks>
    /// A catalogue that packs its entries closely - the reference instruction book leaves as
    /// few as 23 clear rows between one entry and the next - puts the entry above well inside
    /// three label heights, so a band taken on height alone photographs someone else's label.
    /// </remarks>
    public static int CeilingAbove(PixelBounds label, IEnumerable<PixelBounds> others, int pageWidth)
    {
        ArgumentNullException.ThrowIfNull(others);

        var band = BandAbove(label, Math.Max(pageWidth, label.Right + 1), label.Bottom + 1);

        return others
            .Where(other => other.Bottom < label.Top)
            .Where(other => other.Right >= band.Left && other.Left <= band.Right)
            .Select(other => other.Bottom + 1)
            .DefaultIfEmpty(0)
            .Max();
    }

    /// <summary>
    /// Writes the drawing above a label as <c>&lt;part&gt;.png</c>, and says whether it did.
    /// </summary>
    /// <returns>
    /// False when a picture of that part is already there, or when it could not be written -
    /// neither of which is a reason to stop reading a document.
    /// </returns>
    public static bool TryWrite(
        SKBitmap page, PixelBounds label, string directory, string partNumber, int ceiling = 0)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        ArgumentException.ThrowIfNullOrWhiteSpace(partNumber);

        var name = Safe(partNumber);
        if (name.Length == 0)
        {
            return false;
        }

        var path = Path.Combine(directory, name + ".png");

        try
        {
            if (File.Exists(path))
            {
                return false;
            }

            using var crop = RowCrop.Extract(
                page, BandAbove(label, page.Width, page.Height, ceiling), padding: 0, margin: 0);

            Directory.CreateDirectory(directory);
            File.WriteAllBytes(path, RowCrop.ToPng(crop));
            return true;
        }
        catch (Exception ex) when (ex is IOException
                                       or UnauthorizedAccessException
                                       or InvalidOperationException)
        {
            return false;
        }
    }

    /// <summary>
    /// Saves the region an entry could not be read from, and gives back the file's name.
    /// </summary>
    /// <remarks>
    /// Named after the page and the corner it came from, so the same region read again lands on
    /// the same file rather than filling the folder with copies.
    /// </remarks>
    public static string? WriteReviewCrop(
        SKBitmap page, PixelBounds region, string directory, int pageNumber)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);

        var name = FormattableString.Invariant($"p{pageNumber}-{region.Left}-{region.Top}.png");

        try
        {
            // The white margin RowCrop adds by default is wanted here: a person reads a crop
            // better with space around it, for the same reason the recogniser did.
            using var crop = RowCrop.Extract(page, region);

            Directory.CreateDirectory(directory);
            File.WriteAllBytes(Path.Combine(directory, name), RowCrop.ToPng(crop));
            return name;
        }
        catch (Exception ex) when (ex is IOException
                                       or UnauthorizedAccessException
                                       or InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>
    /// Files a picture under the part number once the element it was read as has been resolved.
    /// </summary>
    /// <remarks>
    /// The catalogue looks for a part number, and a page only ever knew an element number.
    /// </remarks>
    public static void Rename(string directory, string from, string to)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);

        try
        {
            var source = Path.Combine(directory, Safe(from) + ".png");
            var target = Path.Combine(directory, Safe(to) + ".png");

            if (File.Exists(source) && !File.Exists(target))
            {
                File.Move(source, target);
            }
            else if (File.Exists(source))
            {
                File.Delete(source);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A picture is a convenience; losing one is never worth stopping a run for.
        }
    }

    private static string Safe(string name) =>
        string.Concat(name.Split(Path.GetInvalidFileNameChars()));
}
