using Lego2STL.Core.Pipeline;
using Lego2STL.Core.Run;

namespace Lego2STL.MobileSmokeTest;

/// <summary>What the smoke test found out, in a shape a screen and a log can both carry.</summary>
public sealed record SmokeResult(bool Passed, string Detail);

/// <summary>
/// A whole run, from a parts list to a plate, with nothing outside this file involved.
/// </summary>
public static class PipelineFixture
{
    // A cube in LDraw's own text format: four type-3 lines are enough to be a shape, and no
    // subfile reference means no library and no network.
    private const string Cube = """
        0 Cube
        0 BFC CERTIFY CCW
        3 16 0 0 0 20 0 0 20 0 20
        3 16 0 0 0 20 0 20 0 0 20
        3 16 0 0 0 0 0 20 0 -20 20
        3 16 0 0 0 0 -20 20 20 0 20
        """;

    // The third column is the BrickLink *colour* code, an integer - not a part code, despite
    // the header's name. Any value parses; nothing downstream looks it up for a made-up part.
    private const string PartsList = """
        ID;Codice Lego;Codice BrickLink;Nome colore;Codice RGB;Quantita
        1;cube;11;Black;#05131D;1
        """;

    public static async Task<SmokeResult> RunAsync(string root, CancellationToken cancellationToken = default)
    {
        try
        {
            var home = new ApplicationStorageRunHome(root);
            var library = Path.Combine(root, "ldraw", "parts");
            Directory.CreateDirectory(library);
            File.WriteAllText(Path.Combine(library, "cube.dat"), Cube);

            var imports = Path.Combine(root, "imports");
            Directory.CreateDirectory(imports);
            var input = Path.Combine(imports, "cube.csv");
            File.WriteAllText(input, PartsList);

            var settings = new RunSettings
            {
                Kind = InputKind.PartsList,
                InputPath = input,
                LDrawDirectory = Path.Combine(root, "ldraw"),
                Offline = true,
                AllowFullArchive = false,
            };

            var outcome = await new PipelineRunner(home: home).RunAsync(settings, cancellationToken);

            var layout = home.Plan(settings)!;
            var shapes = Directory.Exists(layout.StlDirectory)
                ? Directory.GetFiles(layout.StlDirectory, "*.stl")
                : [];
            var plates = Directory.Exists(layout.PlateDirectory)
                ? Directory.GetFiles(layout.PlateDirectory, "*.3mf")
                : [];

            return shapes.Length > 0 && plates.Length > 0
                ? new SmokeResult(true, $"cube: {shapes.Length} shape, {plates.Length} plate, in {layout.Root}")
                : new SmokeResult(false, $"cube: {shapes.Length} shapes and {plates.Length} plates in {layout.Root}");
        }
        catch (Exception ex)
        {
            // The smoke test's own failure has to be as legible as a real one.
            return new SmokeResult(false, ex.GetType().Name + ": " + ex.Message);
        }
    }
}
