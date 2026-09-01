using Lego2STL.Core.Ocr;
using Lego2STL.OcrSmokeTest;

// The whole of the macOS smoke test: no UI at all, because Vision runs headless. Run with
// `dotnet run -f net10.0-macos26.0` and read the result off stdout.
try
{
    var engine = OcrEngines.Create();
    var result = await SyntheticFixture.RunAsync(engine);

    Console.WriteLine(result.Passed
        ? $"PASS ({engine.Name})"
        : $"FAIL ({engine.Name}) - expected \"{result.ExpectedText}\", read \"{result.ActualText}\"");

    Environment.Exit(result.Passed ? 0 : 1);
}
catch (Exception ex)
{
    Console.WriteLine($"FAIL - threw {ex.GetType().Name}: {ex.Message}");
    Environment.Exit(1);
}
