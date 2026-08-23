using System.Numerics;

namespace Lego2STL.Core.Geometry;

/// <summary>
/// A grid over a mesh's vertices, for asking which of them lie near a given place.
/// </summary>
/// <remarks>
/// Needed because the T-junction repair asks, for every edge of every triangle, which other
/// vertices sit on that edge. Done by scanning all vertices that would be the square of the
/// vertex count; over a grid it is proportional to how many vertices are actually nearby.
/// </remarks>
public sealed class VertexGrid
{
    private readonly IReadOnlyList<Vector3> _vertices;
    private readonly Dictionary<(int X, int Y, int Z), List<int>> _cells = [];
    private readonly float _cellSize;

    public VertexGrid(IReadOnlyList<Vector3> vertices, float cellSize = 4f)
    {
        ArgumentNullException.ThrowIfNull(vertices);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(cellSize);

        _vertices = vertices;
        _cellSize = cellSize;

        for (var i = 0; i < vertices.Count; i++)
        {
            var cell = CellOf(vertices[i]);
            if (!_cells.TryGetValue(cell, out var bucket))
            {
                bucket = [];
                _cells[cell] = bucket;
            }

            bucket.Add(i);
        }
    }

    /// <summary>Vertex indices in the cells covering a box, grown by one cell on every side.</summary>
    public IEnumerable<int> Near(Vector3 min, Vector3 max)
    {
        var low = CellOf(min);
        var high = CellOf(max);

        for (var x = low.X - 1; x <= high.X + 1; x++)
        {
            for (var y = low.Y - 1; y <= high.Y + 1; y++)
            {
                for (var z = low.Z - 1; z <= high.Z + 1; z++)
                {
                    if (_cells.TryGetValue((x, y, z), out var bucket))
                    {
                        foreach (var index in bucket)
                        {
                            yield return index;
                        }
                    }
                }
            }
        }
    }

    /// <summary>Vertex indices near the segment between two points.</summary>
    public IEnumerable<int> NearSegment(Vector3 a, Vector3 b) =>
        Near(Vector3.Min(a, b), Vector3.Max(a, b));

    public Vector3 this[int index] => _vertices[index];

    private (int X, int Y, int Z) CellOf(Vector3 point) => (
        (int)MathF.Floor(point.X / _cellSize),
        (int)MathF.Floor(point.Y / _cellSize),
        (int)MathF.Floor(point.Z / _cellSize));
}
