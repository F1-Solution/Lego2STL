using System.Numerics;
using Lego2STL.Core.Geometry;

namespace Lego2STL.Core.LDraw;

/// <summary>A part's surfaces, gathered from its file and everything that file refers to.</summary>
/// <param name="Reference">What was asked for.</param>
/// <param name="Title">The part's description, from its file.</param>
/// <param name="Triangles">Every surface, in the part's own coordinates.</param>
/// <param name="MovedTo">
/// Set when the number asked for is a retired one and the shape actually comes from a
/// replacement part. Worth reporting: the shape is not the number's own.
/// </param>
/// <param name="FilesUsed">How many distinct files contributed.</param>
/// <param name="MissingReferences">Anything referenced that could not be found.</param>
public sealed record PartMesh(
    string Reference,
    string? Title,
    IReadOnlyList<Triangle> Triangles,
    string? MovedTo,
    int FilesUsed,
    IReadOnlyList<string> MissingReferences)
{
    public bool IsEmpty => Triangles.Count == 0;
}

/// <summary>
/// Turns an LDraw part into triangles by following everything it refers to.
/// </summary>
/// <remarks>
/// <para>
/// A part file is mostly references. Its own lines place sub-parts and primitives, those place
/// more, and the surfaces are at the leaves: one panel in the reference set draws on 37 files.
/// So building a part means walking that tree, carrying a transform down it and collecting
/// triangles at the bottom.
/// </para>
/// <para>
/// Two things travel down the walk. The transform, which composes so that each file's own
/// coordinates land in the right place; and a flag for whether surfaces have been turned
/// inside out, which flips at every reference marked for it and at every mirroring transform,
/// so that a part built from mirrored halves still has all its faces pointing outwards.
/// </para>
/// <para>
/// Parsed files are kept, because primitives are referenced over and over - a single part can
/// use the same cylinder segment hundreds of times - and parsing each occurrence would dominate
/// the cost.
/// </para>
/// </remarks>
public sealed class LDrawMeshBuilder
{
    /// <summary>Deep enough for any real part; a guard against a reference cycle in the data.</summary>
    public const int MaxDepth = 48;

    private readonly ILDrawLibrary _library;
    private readonly Dictionary<string, LDrawFile?> _parsed = new(StringComparer.Ordinal);

    public LDrawMeshBuilder(ILDrawLibrary library) =>
        _library = library ?? throw new ArgumentNullException(nameof(library));

    public async Task<PartMesh> BuildAsync(string partNumber, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(partNumber);

        var reference = partNumber.EndsWith(".dat", StringComparison.OrdinalIgnoreCase)
            ? partNumber
            : partNumber + ".dat";

        var root = await ReadAsync(reference, cancellationToken).ConfigureAwait(false)
            ?? throw new LDrawPartNotFoundException(partNumber);

        var state = new BuildState();
        await AddAsync(reference, Matrix4x4.Identity, invert: false, 0, state, cancellationToken)
            .ConfigureAwait(false);

        return new PartMesh(
            partNumber,
            root.Title,
            state.Triangles,
            root.MovedTo,
            state.FilesUsed.Count,
            [.. state.Missing]);
    }

    private sealed class BuildState
    {
        public List<Triangle> Triangles { get; } = [];

        public HashSet<string> FilesUsed { get; } = new(StringComparer.Ordinal);

        public SortedSet<string> Missing { get; } = new(StringComparer.Ordinal);

        /// <summary>Files currently being expanded, so a cycle in the data cannot loop forever.</summary>
        public HashSet<string> InProgress { get; } = new(StringComparer.Ordinal);
    }

    private async Task AddAsync(
        string reference,
        Matrix4x4 transform,
        bool invert,
        int depth,
        BuildState state,
        CancellationToken cancellationToken)
    {
        if (depth > MaxDepth)
        {
            state.Missing.Add($"{reference} (nested deeper than {MaxDepth})");
            return;
        }

        var key = LDrawReference.Normalise(reference);

        if (!state.InProgress.Add(key))
        {
            state.Missing.Add($"{reference} (refers to itself)");
            return;
        }

        try
        {
            var file = await ReadAsync(reference, cancellationToken).ConfigureAwait(false);
            if (file is null)
            {
                state.Missing.Add(key);
                return;
            }

            state.FilesUsed.Add(key);

            // A file can declare its own surfaces wound the other way; that affects its faces
            // but not what it passes down to the files it refers to.
            var facesInverted = invert ^ file.FacesAreClockwise;

            foreach (var instruction in file.Instructions)
            {
                cancellationToken.ThrowIfCancellationRequested();

                switch (instruction)
                {
                    case LDrawFace face:
                        AddFace(state.Triangles, face.Corners, transform, facesInverted);
                        break;

                    case LDrawSubFile sub:
                        await AddAsync(
                            sub.Reference,
                            LDrawMatrix.Combine(sub.Transform, transform),
                            invert ^ sub.Invert,
                            depth + 1,
                            state,
                            cancellationToken).ConfigureAwait(false);
                        break;
                }
            }
        }
        finally
        {
            state.InProgress.Remove(key);
        }
    }

    /// <summary>
    /// Adds a face as triangles. A four-cornered face is split along one diagonal, which is
    /// safe here because the format requires such faces to be flat and convex.
    /// </summary>
    private static void AddFace(List<Triangle> triangles, Vector3[] corners, Matrix4x4 transform, bool invert)
    {
        if (corners.Length < 3)
        {
            return;
        }

        var placed = new Vector3[corners.Length];
        for (var i = 0; i < corners.Length; i++)
        {
            placed[i] = Vector3.Transform(corners[i], transform);
        }

        for (var i = 1; i + 1 < placed.Length; i++)
        {
            var triangle = new Triangle(placed[0], placed[i], placed[i + 1]);
            triangles.Add(invert ? triangle.Reversed() : triangle);
        }
    }

    private async Task<LDrawFile?> ReadAsync(string reference, CancellationToken cancellationToken)
    {
        var key = LDrawReference.Normalise(reference);

        if (_parsed.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var content = await _library.TryReadAsync(reference, cancellationToken).ConfigureAwait(false);
        var parsed = content is null ? null : LDrawFileParser.Parse(content);

        _parsed[key] = parsed;
        return parsed;
    }
}

/// <summary>Thrown when the part asked for is not in any available source.</summary>
public sealed class LDrawPartNotFoundException : Exception
{
    public LDrawPartNotFoundException(string partNumber)
        : base($"No LDraw file for part '{partNumber}'.") =>
        PartNumber = partNumber;

    public string PartNumber { get; }
}
