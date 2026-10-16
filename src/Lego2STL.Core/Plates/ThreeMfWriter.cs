using System.Globalization;
using System.IO.Compression;
using System.Numerics;
using System.Text;
using System.Xml;
using Lego2STL.Core.Colors;
using Lego2STL.Core.Geometry;

namespace Lego2STL.Core.Plates;

/// <summary>One shape, and where copies of it sit on the plate.</summary>
public sealed record PlateObject(string PartNumber, IndexedMesh Mesh, IReadOnlyList<Vector2> Positions);

/// <summary>A plate ready to be written: everything on it is one colour.</summary>
public sealed record PlateContents(
    string Name,
    string ColorName,
    Rgb24 Rgb,
    IReadOnlyList<PlateObject> Objects)
{
    public int PieceCount => Objects.Sum(o => o.Positions.Count);
}

/// <summary>
/// Writes a plate as a 3MF file, with the parts in their real colour.
/// </summary>
/// <remarks>
/// <para>
/// Hand-written, because a 3MF is a zip holding two small fixed files and one piece of XML,
/// and no maintained .NET library for it exists outside a commercial product. Writing it
/// directly is less code than binding to one would be, and leaves nothing to go stale.
/// </para>
/// <para>
/// This is the only place colour reaches the output. STL carries geometry and nothing else,
/// so a set of STLs cannot say that these beams are red and those are black; a 3MF can, and
/// a slicer opening one shows the plate already looking like the finished model.
/// </para>
/// <para>
/// The colour is written twice, in the two places readers look for it. The materials and
/// properties extension's colour group is what the slicers do read - Bambu Studio and Orca
/// among them - and it is what the parts point at, so a plate arrives in a slicer already
/// carrying its colour and offering to map it onto a filament. The base material beside it
/// says the same thing in the core format's own terms, for viewers that only know that one.
/// </para>
/// <para>
/// A shape appears once however many copies are on the plate, with one build item per copy.
/// A colour with a dozen identical pins is then a dozen placements of one mesh rather than a
/// dozen meshes, which is the difference between a small file and a large one.
/// </para>
/// </remarks>
public static class ThreeMfWriter
{
    private const string CoreNamespace = "http://schemas.microsoft.com/3dmanufacturing/core/2015/02";

    /// <summary>
    /// The materials and properties extension. Optional, so it is not declared as required:
    /// a reader that does not know it still gets the whole plate, without the colour.
    /// </summary>
    private const string MaterialNamespace =
        "http://schemas.microsoft.com/3dmanufacturing/material/2015/02";

    /// <summary>
    /// The prefix the colour group is written under. Spelled out because readers match the
    /// element name as written rather than resolving the namespace, so "m" is part of the
    /// contract and not a detail of how this file happens to be produced.
    /// </summary>
    private const string MaterialPrefix = "m";

    private const string ContentTypesNamespace =
        "http://schemas.openxmlformats.org/package/2006/content-types";

    private const string RelationshipsNamespace =
        "http://schemas.openxmlformats.org/package/2006/relationships";

    private const string ModelRelationshipType =
        "http://schemas.microsoft.com/3dmanufacturing/2013/01/3dmodel";

    private const string ModelPath = "3D/3dmodel.model";

    /// <summary>Where a Bambu Studio or OrcaSlicer project keeps its plates (#12).</summary>
    internal const string ModelSettingsPath = "Metadata/model_settings.config";

    /// <summary>Where a Bambu Studio or OrcaSlicer project keeps the presets it names (#12).</summary>
    internal const string ProjectSettingsPath = "Metadata/project_settings.config";

    /// <summary>The base material's id. Every resource in the file needs a distinct one.</summary>
    private const int MaterialResourceId = 1;

    /// <summary>The colour group's id: what the parts point at, and what slicers read.</summary>
    private const int ColorResourceId = 2;

    /// <summary>
    /// A fixed timestamp on every entry, so that writing the same plate twice produces the
    /// same bytes. Without it a zip records the moment it was made and nothing can be compared.
    /// </summary>
    private static readonly DateTimeOffset FixedTimestamp =
        new(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public static byte[] Write(PlateContents plate)
    {
        ArgumentNullException.ThrowIfNull(plate);

        return Write(plate.Name, [plate], 0f);
    }

    /// <summary>
    /// Several plates in one file, each in its own colour, laid side by side along X (#5).
    /// </summary>
    /// <param name="stride">How far apart the plates' origins sit, so that no two overlap.</param>
    public static byte[] Write(string name, IReadOnlyList<PlateContents> plates, float stride)
    {
        ArgumentNullException.ThrowIfNull(plates);

        return Write(name, plates, [.. plates.Select((_, p) => new Vector2(stride * p, 0f))], null);
    }

    /// <summary>
    /// Several plates as a slicer project, each on the slicer plate of its own (#12).
    /// </summary>
    public static byte[] Write(string name, IReadOnlyList<PlateContents> plates, SlicerProject project)
    {
        ArgumentNullException.ThrowIfNull(plates);
        ArgumentNullException.ThrowIfNull(project);

        return Write(
            name, plates, [.. plates.Select((_, p) => project.PlateOrigin(p, plates.Count))], project);
    }

    private static byte[] Write(
        string name,
        IReadOnlyList<PlateContents> plates,
        IReadOnlyList<Vector2> offsets,
        SlicerProject? project)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        // One plate is described exactly as on its own, so it produces the same bytes.
        var description = plates.Count == 1
            ? $"{plates[0].ColorName} ({plates[0].Rgb})"
            : string.Join(", ", plates.Select(p => p.ColorName).Distinct(StringComparer.Ordinal));

        var placed = Place(plates);

        using var buffer = new MemoryStream();

        using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            AddEntry(archive, "[Content_Types].xml", ContentTypes());
            AddEntry(archive, "_rels/.rels", Relationships());
            AddEntry(archive, ModelPath, Model(name, description, plates, placed, offsets, project));

            if (project is not null)
            {
                var (filaments, plateFilament) = project.Filaments(plates);
                var objects = placed
                    .Select(p => new ProjectObject(p.Id, p.Part.PartNumber, p.Plate, p.Part.Positions.Count))
                    .ToList();

                AddEntry(archive, ModelSettingsPath, project.ModelSettings(plates, objects, plateFilament));
                AddEntry(archive, ProjectSettingsPath, project.ProjectSettings(filaments));
            }
        }

        return buffer.ToArray();
    }

    public static async Task WriteFileAsync(
        string path,
        string name,
        IReadOnlyList<PlateContents> plates,
        float stride,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);

        await File.WriteAllBytesAsync(path, Write(name, plates, stride), cancellationToken)
            .ConfigureAwait(false);
    }

    public static async Task WriteFileAsync(
        string path,
        string name,
        IReadOnlyList<PlateContents> plates,
        SlicerProject project,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);

        await File.WriteAllBytesAsync(path, Write(name, plates, project), cancellationToken)
            .ConfigureAwait(false);
    }

    public static async Task WriteFileAsync(
        string path,
        PlateContents plate,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);

        await File.WriteAllBytesAsync(path, Write(plate), cancellationToken).ConfigureAwait(false);
    }

    private static void AddEntry(ZipArchive archive, string name, string content)
    {
        var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
        entry.LastWriteTime = FixedTimestamp;

        using var stream = entry.Open();
        var bytes = new UTF8Encoding(false).GetBytes(content);
        stream.Write(bytes, 0, bytes.Length);
    }

    private static string ContentTypes() =>
        WriteXml(w =>
        {
            w.WriteStartElement("Types", ContentTypesNamespace);

            w.WriteStartElement("Default", ContentTypesNamespace);
            w.WriteAttributeString("Extension", "rels");
            w.WriteAttributeString(
                "ContentType", "application/vnd.openxmlformats-package.relationships+xml");
            w.WriteEndElement();

            w.WriteStartElement("Default", ContentTypesNamespace);
            w.WriteAttributeString("Extension", "model");
            w.WriteAttributeString(
                "ContentType", "application/vnd.ms-package.3dmanufacturing-3dmodel+xml");
            w.WriteEndElement();

            w.WriteEndElement();
        });

    private static string Relationships() =>
        WriteXml(w =>
        {
            w.WriteStartElement("Relationships", RelationshipsNamespace);

            w.WriteStartElement("Relationship", RelationshipsNamespace);
            w.WriteAttributeString("Id", "rel0");
            w.WriteAttributeString("Type", ModelRelationshipType);
            w.WriteAttributeString("Target", "/" + ModelPath);
            w.WriteEndElement();

            w.WriteEndElement();
        });

    /// <summary>Every shape on every plate, with the object id it is written under.</summary>
    private static List<(int Id, PlateObject Part, int Plate)> Place(IReadOnlyList<PlateContents> plates)
    {
        var placed = new List<(int Id, PlateObject Part, int Plate)>();
        var objectId = ColorResourceId;

        for (var p = 0; p < plates.Count; p++)
        {
            foreach (var part in plates[p].Objects)
            {
                placed.Add((++objectId, part, p));
            }
        }

        return placed;
    }

    private static string Model(
        string name,
        string description,
        IReadOnlyList<PlateContents> plates,
        IReadOnlyList<(int Id, PlateObject Part, int Plate)> placed,
        IReadOnlyList<Vector2> offsets,
        SlicerProject? project) =>
        WriteXml(w =>
        {
            w.WriteStartElement("model", CoreNamespace);
            w.WriteAttributeString("unit", "millimeter");
            w.WriteAttributeString("xml", "lang", null, "en-US");
            w.WriteAttributeString("xmlns", MaterialPrefix, null, MaterialNamespace);

            foreach (var (key, value) in project?.ModelMetadata() ?? [])
            {
                WriteMetadata(w, key, value);
            }

            WriteMetadata(w, "Title", name);
            WriteMetadata(w, "Designer", "Lego2STL");
            WriteMetadata(w, "Description", description);

            w.WriteStartElement("resources", CoreNamespace);

            // One colour per plate: everything on a plate is the same colour, which is the
            // point of grouping plates by colour in the first place. A combined file lists
            // each plate's colour in plate order, and every part points at its own plate's.
            w.WriteStartElement("basematerials", CoreNamespace);
            w.WriteAttributeString("id", Number(MaterialResourceId));
            foreach (var plate in plates)
            {
                w.WriteStartElement("base", CoreNamespace);
                w.WriteAttributeString("name", plate.ColorName);
                w.WriteAttributeString("displaycolor", DisplayColor(plate.Rgb));
                w.WriteEndElement();
            }

            w.WriteEndElement();

            w.WriteStartElement(MaterialPrefix, "colorgroup", MaterialNamespace);
            w.WriteAttributeString("id", Number(ColorResourceId));
            foreach (var plate in plates)
            {
                w.WriteStartElement(MaterialPrefix, "color", MaterialNamespace);
                w.WriteAttributeString("color", DisplayColor(plate.Rgb));
                w.WriteEndElement();
            }

            w.WriteEndElement();

            foreach (var (id, part, plate) in placed)
            {
                WriteObject(w, id, part, plate);
            }

            w.WriteEndElement();   // resources

            w.WriteStartElement("build", CoreNamespace);

            foreach (var (id, part, plate) in placed)
            {
                foreach (var position in part.Positions)
                {
                    w.WriteStartElement("item", CoreNamespace);
                    w.WriteAttributeString("objectid", Number(id));
                    w.WriteAttributeString("transform", Translation(position + offsets[plate]));
                    w.WriteEndElement();
                }
            }

            w.WriteEndElement();   // build
            w.WriteEndElement();   // model
        });

    private static void WriteMetadata(XmlWriter w, string name, string value)
    {
        w.WriteStartElement("metadata", CoreNamespace);
        w.WriteAttributeString("name", name);
        w.WriteString(value);
        w.WriteEndElement();
    }

    private static void WriteObject(XmlWriter w, int id, PlateObject part, int colorIndex)
    {
        w.WriteStartElement("object", CoreNamespace);
        w.WriteAttributeString("id", Number(id));
        w.WriteAttributeString("name", part.PartNumber);
        w.WriteAttributeString("type", "model");
        w.WriteAttributeString("pid", Number(ColorResourceId));
        w.WriteAttributeString("pindex", Number(colorIndex));

        w.WriteStartElement("mesh", CoreNamespace);

        w.WriteStartElement("vertices", CoreNamespace);
        foreach (var vertex in part.Mesh.Vertices)
        {
            w.WriteStartElement("vertex", CoreNamespace);
            w.WriteAttributeString("x", Coordinate(vertex.X));
            w.WriteAttributeString("y", Coordinate(vertex.Y));
            w.WriteAttributeString("z", Coordinate(vertex.Z));
            w.WriteEndElement();
        }

        w.WriteEndElement();

        w.WriteStartElement("triangles", CoreNamespace);
        foreach (var triangle in part.Mesh.Triangles)
        {
            if (triangle.IsDegenerate)
            {
                continue;
            }

            w.WriteStartElement("triangle", CoreNamespace);
            w.WriteAttributeString("v1", Number(triangle.A));
            w.WriteAttributeString("v2", Number(triangle.B));
            w.WriteAttributeString("v3", Number(triangle.C));
            w.WriteEndElement();
        }

        w.WriteEndElement();
        w.WriteEndElement();   // mesh
        w.WriteEndElement();   // object
    }

    /// <summary>
    /// The build transform: three rows of the rotation, then the translation. Nothing is
    /// rotated here - the shapes already stand the right way up - so it is the identity plus
    /// a move to the part's place on the bed.
    /// </summary>
    private static string Translation(Vector2 position) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"1 0 0 0 1 0 0 0 1 {position.X:0.####} {position.Y:0.####} 0");

    /// <summary>Colour as the format writes it: hex with an alpha channel.</summary>
    private static string DisplayColor(Rgb24 rgb) =>
        string.Create(CultureInfo.InvariantCulture, $"#{rgb.R:X2}{rgb.G:X2}{rgb.B:X2}FF");

    private static string Coordinate(float value) =>
        value.ToString("0.####", CultureInfo.InvariantCulture);

    private static string Number(int value) =>
        value.ToString(CultureInfo.InvariantCulture);

    internal static string WriteXml(Action<XmlWriter> body)
    {
        var settings = new XmlWriterSettings
        {
            Indent = false,
            Encoding = new UTF8Encoding(false),
            OmitXmlDeclaration = false,
            NewLineChars = "\n",
        };

        var text = new StringWriterWithEncoding(new UTF8Encoding(false));

        using (var writer = XmlWriter.Create(text, settings))
        {
            body(writer);
        }

        return text.ToString();
    }

    /// <summary>
    /// A string writer that reports UTF-8, so the XML declaration says so. The default one
    /// claims UTF-16 because that is what a .NET string is, which is true of the string and
    /// wrong for the file it becomes.
    /// </summary>
    private sealed class StringWriterWithEncoding(Encoding encoding) : StringWriter
    {
        public override Encoding Encoding { get; } = encoding;
    }
}
