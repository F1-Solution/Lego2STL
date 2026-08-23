using System.Numerics;

namespace Lego2STL.Core.Geometry;

/// <summary>What happened when a shape was taken in.</summary>
/// <param name="Mesh">The result. The original, unchanged, when it could not be done.</param>
/// <param name="Applied">True when every face really was moved.</param>
/// <param name="Reason">Why not, when it was not. Null on success.</param>
public sealed record ClearanceResult(IndexedMesh Mesh, bool Applied, string? Reason)
{
    public static ClearanceResult Unchanged(IndexedMesh mesh, string reason) =>
        new(mesh, false, reason);
}

/// <summary>
/// Takes every face of a shape in by a fixed distance, so a printed part has the clearance a
/// moulded one is made with.
/// </summary>
/// <remarks>
/// <para>
/// The catalogue's geometry is nominal: an axle is exactly 4.8 mm across and the hole it goes
/// into is exactly 4.8 mm wide, because on paper they are the same dimension. A real moulded
/// pair differs by a few hundredths, and that difference is the whole of the fit. Printed as
/// drawn, nothing goes into anything.
/// </para>
/// <para>
/// Moving every face inward by the same amount is the correction. Where a corner has to go is
/// then a question with one equation per face through it: each face must end up the requested
/// distance nearer. A box corner has three and is pinned exactly; a corner along an edge has
/// two and could slide anywhere along that edge; one in the middle of a flat face has one and
/// could slide anywhere across it; one around a cylinder has twenty. Solving for the smallest
/// answer handles all of them: the corner moves as the faces require and not one bit further,
/// so nothing slides sideways for no reason.
/// </para>
/// <para>
/// Averaging the face directions instead would be wrong, and quietly so. The average is
/// weighted by how many triangles happen to meet at the corner, and a box corner is touched
/// by one triangle of one face and two of the others purely because of how the square faces
/// were cut in half. The faces would then come in by different amounts depending on the
/// triangulation - measured on a 20 mm cube asking for 0.15 mm, that error was more than half.
/// </para>
/// <para>
/// Two things stop it. A shape with holes in its surface has no inside for a face to move
/// towards along its boundary, so the result would be distorted rather than smaller; and a
/// wall already thinner than twice the distance would be turned inside out. Both are reported
/// by name and the shape is left at true size, which is far better than quietly shipping a
/// part that is wrong in a way nobody will see until it is printed.
/// </para>
/// </remarks>
public static class ClearanceOffset
{
    /// <summary>
    /// The furthest a corner may travel, as a multiple of the clearance. A very sharp spike
    /// needs its tip moved a long way to bring its sides in a little, and past this point the
    /// tip is doing more harm than the clearance is doing good, so it is held back. Its
    /// neighbours still move, so the faces still come in.
    /// </summary>
    private const float MaximumTravel = 8f;

    public static ClearanceResult Apply(IndexedMesh mesh, float millimetres, MeshQuality quality)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        ArgumentNullException.ThrowIfNull(quality);

        if (millimetres < 0f)
        {
            throw new ArgumentOutOfRangeException(
                nameof(millimetres), millimetres, "A clearance cannot be negative.");
        }

        if (millimetres == 0f || mesh.VertexCount == 0)
        {
            return new ClearanceResult(mesh, Applied: false, Reason: null);
        }

        if (!quality.IsClosed)
        {
            return ClearanceResult.Unchanged(mesh, "open");
        }

        var (min, max) = mesh.Bounds();
        var thinnest = MathF.Min(MathF.Min(max.X - min.X, max.Y - min.Y), max.Z - min.Z);

        if (thinnest <= 2f * millimetres)
        {
            return ClearanceResult.Unchanged(mesh, "thin");
        }

        var faces = FacesAtEachCorner(mesh);
        var moved = new Vector3[mesh.VertexCount];
        var limit = millimetres * MaximumTravel;

        for (var i = 0; i < mesh.VertexCount; i++)
        {
            moved[i] = mesh.Vertices[i] + Displacement(faces[i], millimetres, limit);
        }

        return new ClearanceResult(new IndexedMesh(moved, mesh.Triangles), Applied: true, Reason: null);
    }

    /// <summary>
    /// Where one corner has to go so that every face through it comes in by the given distance.
    /// </summary>
    /// <remarks>
    /// Each face contributes the requirement that the corner move by the distance against that
    /// face's direction. Written out, that is a matrix built from the directions and a vector
    /// built from the distance, and solved for the smallest answer so that directions no face
    /// mentions are left alone.
    /// </remarks>
    private static Vector3 Displacement(IReadOnlyList<Vector3> normals, float millimetres, float limit)
    {
        if (normals.Count == 0)
        {
            return Vector3.Zero;
        }

        float a00 = 0f, a01 = 0f, a02 = 0f, a11 = 0f, a12 = 0f, a22 = 0f;
        var rightHandSide = Vector3.Zero;

        foreach (var n in normals)
        {
            a00 += n.X * n.X;
            a01 += n.X * n.Y;
            a02 += n.X * n.Z;
            a11 += n.Y * n.Y;
            a12 += n.Y * n.Z;
            a22 += n.Z * n.Z;

            rightHandSide -= n * millimetres;
        }

        var eigen = SymmetricEigen3.Decompose(a00, a01, a02, a11, a12, a22);
        var displacement = SymmetricEigen3.SolveSmallest(eigen, rightHandSide);

        var travel = displacement.Length();
        return travel > limit ? displacement * (limit / travel) : displacement;
    }

    /// <summary>
    /// The distinct directions the faces meeting at each corner point in.
    /// </summary>
    /// <remarks>
    /// Distinct is the important word. A square face arrives as two triangles that point the
    /// same way, and counting it twice would weight that face double against its neighbours,
    /// which is precisely the error that averaging makes.
    /// </remarks>
    private static List<Vector3>[] FacesAtEachCorner(IndexedMesh mesh)
    {
        var perCorner = new List<Vector3>[mesh.VertexCount];
        for (var i = 0; i < perCorner.Length; i++)
        {
            perCorner[i] = [];
        }

        foreach (var triangle in mesh.Triangles)
        {
            if (triangle.IsDegenerate)
            {
                continue;
            }

            var normal = mesh.ToTriangle(triangle).Normal();

            if (normal.LengthSquared() < 1e-12f)
            {
                continue;
            }

            normal = Vector3.Normalize(normal);

            foreach (var corner in triangle.Corners())
            {
                AddIfNew(perCorner[corner], normal);
            }
        }

        return perCorner;
    }

    /// <summary>
    /// How nearly parallel two face directions have to be to count as the same face. About a
    /// quarter of a degree: enough to merge the halves of a square, far short of merging the
    /// neighbouring facets of a cylinder, whose finest steps here are several degrees apart.
    /// </summary>
    private const float SameFaceDot = 0.99999f;

    private static void AddIfNew(List<Vector3> normals, Vector3 normal)
    {
        foreach (var existing in normals)
        {
            if (Vector3.Dot(existing, normal) >= SameFaceDot)
            {
                return;
            }
        }

        normals.Add(normal);
    }

    /// <summary>The smallest span of a shape's box, which is what a clearance has to stay under.</summary>
    public static float ThinnestSpan(IndexedMesh mesh)
    {
        ArgumentNullException.ThrowIfNull(mesh);

        var (min, max) = mesh.Bounds();
        return MathF.Min(MathF.Min(max.X - min.X, max.Y - min.Y), max.Z - min.Z);
    }
}
