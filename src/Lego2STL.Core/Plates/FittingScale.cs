namespace Lego2STL.Core.Plates;

/// <summary>
/// The largest scale at which every part still fits the plate.
/// </summary>
/// <remarks>
/// Measured against the same usable area the packer measures against - the bed less its margin
/// on both sides - because a scale the packer would then reject is worse than no suggestion at
/// all. Rounded down for the same reason.
/// </remarks>
public static class FittingScale
{
    /// <param name="scaleUsed">The percentage the run was made at, which the items are already at.</param>
    /// <returns>The largest whole percent that fits, or null when everything already fits.</returns>
    public static double? Largest(
        IEnumerable<PackableItem> items,
        PrintBed bed,
        float margin,
        double scaleUsed)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(bed);

        var usableWidth = bed.Width - (2f * margin);
        var usableDepth = bed.Depth - (2f * margin);

        var worst = double.MaxValue;
        var any = false;

        foreach (var item in items)
        {
            any = true;

            var factor = Math.Min(
                Math.Min(Room(usableWidth, item.Footprint.X), Room(usableDepth, item.Footprint.Y)),
                Room(bed.Height, item.Height));

            worst = Math.Min(worst, factor);
        }

        if (!any || worst >= 1)
        {
            return null;
        }

        var suggested = Math.Floor(scaleUsed * worst);
        return suggested < 1 ? 1 : suggested;
    }

    /// <summary>How much of the room a measurement leaves; more than one means it fits.</summary>
    private static double Room(float available, float needed) =>
        needed <= 0 ? double.MaxValue : available / needed;
}
