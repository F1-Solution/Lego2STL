using System.Buffers.Binary;
using System.Globalization;
using System.Numerics;
using System.Text;

namespace Lego2STL.Core.Geometry;

/// <summary>
/// Writes a mesh as an STL file, in either the compact or the readable form.
/// </summary>
/// <remarks>
/// <para>
/// Hand-written rather than taken from a library, because the format is small enough that a
/// dependency would add more to maintain than it removes: a fixed header, a count, then fifty
/// bytes per triangle.
/// </para>
/// <para>
/// The compact form is the default. It is about six times smaller than the readable one and
/// is accepted everywhere; the readable form exists for when a file needs to be inspected by
/// eye or compared line by line.
/// </para>
/// </remarks>
public static class StlWriter
{
    private const int HeaderBytes = 80;
    private const int BytesPerTriangle = 50;

    /// <summary>Writes the compact form: an 80-byte header, a triangle count, then the triangles.</summary>
    public static byte[] WriteBinary(IndexedMesh mesh, string? headerText = null)
    {
        ArgumentNullException.ThrowIfNull(mesh);

        var triangles = mesh.ToTriangles().Where(t => !t.IsDegenerate()).ToList();

        var buffer = new byte[HeaderBytes + 4 + (triangles.Count * BytesPerTriangle)];

        // The header is free-form; a note of where the file came from is more use than zeroes.
        var header = Encoding.ASCII.GetBytes(headerText ?? "Lego2STL");
        Array.Copy(header, buffer, Math.Min(header.Length, HeaderBytes - 1));

        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(HeaderBytes), (uint)triangles.Count);

        var at = HeaderBytes + 4;

        foreach (var triangle in triangles)
        {
            WriteVector(buffer, ref at, triangle.Normal());
            WriteVector(buffer, ref at, triangle.A);
            WriteVector(buffer, ref at, triangle.B);
            WriteVector(buffer, ref at, triangle.C);

            // The trailing pair of bytes has no agreed meaning; zero is what readers expect.
            BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(at), 0);
            at += 2;
        }

        return buffer;
    }

    /// <summary>Writes the readable form.</summary>
    public static string WriteText(IndexedMesh mesh, string name = "part")
    {
        ArgumentNullException.ThrowIfNull(mesh);

        var sb = new StringBuilder();
        var safeName = string.IsNullOrWhiteSpace(name) ? "part" : name;

        sb.Append("solid ").Append(safeName).Append('\n');

        foreach (var triangle in mesh.ToTriangles().Where(t => !t.IsDegenerate()))
        {
            var n = triangle.Normal();
            sb.Append("  facet normal ").Append(Format(n)).Append('\n');
            sb.Append("    outer loop\n");
            sb.Append("      vertex ").Append(Format(triangle.A)).Append('\n');
            sb.Append("      vertex ").Append(Format(triangle.B)).Append('\n');
            sb.Append("      vertex ").Append(Format(triangle.C)).Append('\n');
            sb.Append("    endloop\n");
            sb.Append("  endfacet\n");
        }

        sb.Append("endsolid ").Append(safeName).Append('\n');
        return sb.ToString();
    }

    public static async Task WriteFileAsync(
        string path,
        IndexedMesh mesh,
        bool asText = false,
        string? name = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);

        if (asText)
        {
            await File.WriteAllTextAsync(path, WriteText(mesh, name ?? "part"), cancellationToken)
                .ConfigureAwait(false);
        }
        else
        {
            await File.WriteAllBytesAsync(path, WriteBinary(mesh, name), cancellationToken)
                .ConfigureAwait(false);
        }
    }

    /// <summary>Triangles in a compact-form file, read back. Used by the tests.</summary>
    public static int ReadBinaryTriangleCount(byte[] contents)
    {
        ArgumentNullException.ThrowIfNull(contents);

        if (contents.Length < HeaderBytes + 4)
        {
            throw new FormatException("Too short to be an STL file.");
        }

        return (int)BinaryPrimitives.ReadUInt32LittleEndian(contents.AsSpan(HeaderBytes));
    }

    private static void WriteVector(byte[] buffer, ref int at, Vector3 v)
    {
        BinaryPrimitives.WriteSingleLittleEndian(buffer.AsSpan(at), v.X);
        BinaryPrimitives.WriteSingleLittleEndian(buffer.AsSpan(at + 4), v.Y);
        BinaryPrimitives.WriteSingleLittleEndian(buffer.AsSpan(at + 8), v.Z);
        at += 12;
    }

    private static string Format(Vector3 v) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{v.X:0.000000e+00} {v.Y:0.000000e+00} {v.Z:0.000000e+00}");
}
