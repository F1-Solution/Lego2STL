namespace Lego2STL.Core.Extraction;

/// <summary>One connected region of ink: its bounding box and how many pixels it holds.</summary>
public sealed record InkComponent(PixelBounds Bounds, int PixelCount)
{
    public int Width => Bounds.Width;

    public int Height => Bounds.Height;

    /// <summary>
    /// Average ink per pixel of area. Separates real glyphs from the thin specks that
    /// appear inside line-art renders.
    /// </summary>
    public double Density => PixelCount / (double)(Bounds.Width * Bounds.Height);
}

/// <summary>
/// Finds 8-connected regions of ink.
/// </summary>
/// <remarks>
/// Works on horizontal runs joined by union-find rather than a per-pixel flood fill. A page
/// is around two million pixels but only a few thousand runs, so this keeps the pass linear
/// in the amount of ink rather than in the size of the page.
/// </remarks>
public static class ConnectedComponents
{
    public static IReadOnlyList<InkComponent> Find(InkMask mask)
    {
        ArgumentNullException.ThrowIfNull(mask);

        var runs = CollectRuns(mask);
        if (runs.Count == 0)
        {
            return [];
        }

        var parent = new int[runs.Count];
        for (var i = 0; i < parent.Length; i++)
        {
            parent[i] = i;
        }

        JoinVerticallyAdjacentRuns(runs, parent);

        return Aggregate(runs, parent);
    }

    private readonly record struct Run(int Y, int Left, int Right);

    private static List<Run> CollectRuns(InkMask mask)
    {
        var runs = new List<Run>();

        for (var y = 0; y < mask.Height; y++)
        {
            var x = 0;
            while (x < mask.Width)
            {
                if (!mask[x, y])
                {
                    x++;
                    continue;
                }

                var start = x;
                while (x < mask.Width && mask[x, y])
                {
                    x++;
                }

                runs.Add(new Run(y, start, x - 1));
            }
        }

        return runs;
    }

    /// <summary>
    /// Unions runs that touch across consecutive rows. Runs are produced in row order, so
    /// each row's slice can be found once and compared only against the row above.
    /// </summary>
    private static void JoinVerticallyAdjacentRuns(List<Run> runs, int[] parent)
    {
        var rowStart = 0;
        var previousRowStart = -1;
        var previousRowEnd = -1;

        while (rowStart < runs.Count)
        {
            var y = runs[rowStart].Y;
            var rowEnd = rowStart;
            while (rowEnd + 1 < runs.Count && runs[rowEnd + 1].Y == y)
            {
                rowEnd++;
            }

            if (previousRowStart >= 0 && runs[previousRowStart].Y == y - 1)
            {
                for (var i = rowStart; i <= rowEnd; i++)
                {
                    for (var j = previousRowStart; j <= previousRowEnd; j++)
                    {
                        // +/-1 makes this 8-connected rather than 4-connected: diagonal
                        // touches count, which matters for thin diagonal glyph strokes.
                        if (runs[i].Left <= runs[j].Right + 1 && runs[j].Left <= runs[i].Right + 1)
                        {
                            Union(parent, i, j);
                        }
                    }
                }
            }

            previousRowStart = rowStart;
            previousRowEnd = rowEnd;
            rowStart = rowEnd + 1;
        }
    }

    private static List<InkComponent> Aggregate(List<Run> runs, int[] parent)
    {
        var byRoot = new Dictionary<int, (int Left, int Top, int Right, int Bottom, int Count)>();

        for (var i = 0; i < runs.Count; i++)
        {
            var run = runs[i];
            var root = Find(parent, i);
            var length = run.Right - run.Left + 1;

            if (byRoot.TryGetValue(root, out var acc))
            {
                byRoot[root] = (
                    Math.Min(acc.Left, run.Left),
                    Math.Min(acc.Top, run.Y),
                    Math.Max(acc.Right, run.Right),
                    Math.Max(acc.Bottom, run.Y),
                    acc.Count + length);
            }
            else
            {
                byRoot[root] = (run.Left, run.Y, run.Right, run.Y, length);
            }
        }

        return byRoot.Values
            .Select(a => new InkComponent(new PixelBounds(a.Left, a.Top, a.Right, a.Bottom), a.Count))
            .ToList();
    }

    private static int Find(int[] parent, int i)
    {
        while (parent[i] != i)
        {
            parent[i] = parent[parent[i]];   // path halving
            i = parent[i];
        }

        return i;
    }

    private static void Union(int[] parent, int a, int b)
    {
        var rootA = Find(parent, a);
        var rootB = Find(parent, b);
        if (rootA != rootB)
        {
            parent[rootB] = rootA;
        }
    }
}
