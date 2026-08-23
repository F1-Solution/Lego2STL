using System.Globalization;
using System.Numerics;

namespace Lego2STL.Core.LDraw;

/// <summary>Something to do when building a mesh from a file.</summary>
public abstract record LDrawInstruction;

/// <summary>A reference to another file, placed by a transform.</summary>
/// <param name="Transform">Where to put it, already in this file's convention.</param>
/// <param name="Invert">
/// Whether the reference was marked as needing its faces reversed, or has a mirroring
/// transform. Both reverse which way its surfaces point.
/// </param>
/// <param name="Reference">The referenced file's name, as written.</param>
public sealed record LDrawSubFile(Matrix4x4 Transform, bool Invert, string Reference) : LDrawInstruction;

/// <summary>A flat face: three or four corners in this file's coordinates.</summary>
public sealed record LDrawFace(Vector3[] Corners) : LDrawInstruction;

/// <summary>One parsed LDraw file.</summary>
/// <param name="Title">The description on the first line, when there is one.</param>
/// <param name="MovedTo">Set when the file is only a redirection to a replacement part.</param>
/// <param name="Instructions">References and faces, in the order they appear.</param>
/// <param name="FacesAreClockwise">
/// True when the file declares its faces wound clockwise, which reverses which way they face.
/// </param>
public sealed record LDrawFile(
    string? Title,
    string? MovedTo,
    IReadOnlyList<LDrawInstruction> Instructions,
    bool FacesAreClockwise);

/// <summary>
/// Reads the lines of an LDraw file into instructions.
/// </summary>
/// <remarks>
/// Only surfaces are kept. The format also carries edge lines and conditional edge lines,
/// which exist to draw outlines on screen and are not part of the shape, so they are skipped;
/// including them would put stray zero-thickness slivers into the output.
/// </remarks>
public static class LDrawFileParser
{
    public static LDrawFile Parse(string content)
    {
        ArgumentNullException.ThrowIfNull(content);

        string? title = null;
        var instructions = new List<LDrawInstruction>();
        var facesAreClockwise = false;
        var invertNext = false;
        var first = true;

        foreach (var rawLine in content.Split('\n'))
        {
            var line = rawLine.Trim();

            if (line.Length == 0)
            {
                continue;
            }

            var fields = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

            switch (fields[0])
            {
                case "0":
                    if (first && fields.Length > 1)
                    {
                        title = line[1..].Trim();
                    }

                    ReadMetaCommand(fields, ref facesAreClockwise, ref invertNext);
                    break;

                case "1":
                    if (TryReadSubFile(fields, invertNext, out var subFile))
                    {
                        instructions.Add(subFile);
                    }

                    // The marker applies to the next reference only, whether or not it parsed.
                    invertNext = false;
                    break;

                case "3":
                    AddFace(instructions, fields, corners: 3);
                    break;

                case "4":
                    AddFace(instructions, fields, corners: 4);
                    break;

                // 2 and 5 are edge lines and conditional edge lines: outlines, not surfaces.
                default:
                    break;
            }

            first = false;
        }

        return new LDrawFile(title, LDrawReference.TryReadMovedTo(content), instructions, facesAreClockwise);
    }

    private static void ReadMetaCommand(string[] fields, ref bool facesAreClockwise, ref bool invertNext)
    {
        if (fields.Length < 2 || !fields[1].Equals("BFC", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        foreach (var token in fields.Skip(2))
        {
            if (token.Equals("INVERTNEXT", StringComparison.OrdinalIgnoreCase))
            {
                invertNext = true;
            }
            else if (token.Equals("CW", StringComparison.OrdinalIgnoreCase))
            {
                facesAreClockwise = true;
            }
            else if (token.Equals("CCW", StringComparison.OrdinalIgnoreCase))
            {
                facesAreClockwise = false;
            }
        }
    }

    private static bool TryReadSubFile(string[] fields, bool invertNext, out LDrawSubFile subFile)
    {
        subFile = null!;

        // 1 <colour> x y z a b c d e f g h i <file>
        if (fields.Length < 15)
        {
            return false;
        }

        var numbers = new float[12];
        for (var i = 0; i < 12; i++)
        {
            if (!TryReadNumber(fields[i + 2], out numbers[i]))
            {
                return false;
            }
        }

        // The file name can contain spaces, so it is everything after the numbers.
        var reference = string.Join(' ', fields.Skip(14));
        if (reference.Length == 0)
        {
            return false;
        }

        var determinant = LDrawMatrix.Determinant(
            numbers[3], numbers[4], numbers[5],
            numbers[6], numbers[7], numbers[8],
            numbers[9], numbers[10], numbers[11]);

        var transform = LDrawMatrix.FromReferenceLine(
            numbers[0], numbers[1], numbers[2],
            numbers[3], numbers[4], numbers[5],
            numbers[6], numbers[7], numbers[8],
            numbers[9], numbers[10], numbers[11]);

        // A mirroring transform reverses which way surfaces face, exactly as the explicit
        // marker does, so the two combine.
        subFile = new LDrawSubFile(transform, invertNext ^ (determinant < 0), reference);
        return true;
    }

    private static void AddFace(List<LDrawInstruction> instructions, string[] fields, int corners)
    {
        var needed = 2 + (corners * 3);
        if (fields.Length < needed)
        {
            return;
        }

        var vertices = new Vector3[corners];

        for (var i = 0; i < corners; i++)
        {
            var at = 2 + (i * 3);

            if (!TryReadNumber(fields[at], out var x) ||
                !TryReadNumber(fields[at + 1], out var y) ||
                !TryReadNumber(fields[at + 2], out var z))
            {
                return;
            }

            vertices[i] = new Vector3(x, y, z);
        }

        instructions.Add(new LDrawFace(vertices));
    }

    private static bool TryReadNumber(string text, out float value) =>
        float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
}
