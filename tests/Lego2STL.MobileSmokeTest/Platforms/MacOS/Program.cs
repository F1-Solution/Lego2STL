using Lego2STL.MobileSmokeTest;

// The whole of the macOS smoke test: no UI at all. Run with `dotnet run -f net10.0-macos26.0`
// and read the result off stdout - the same shape as the OCR smoke test's macOS host.
var root = Path.Combine(Path.GetTempPath(), "lego2stl-pipeline-smoke");
var result = await PipelineFixture.RunAsync(root);
var line = (result.Passed ? "SMOKE PASS " : "SMOKE FAIL ") + result.Detail;

Console.WriteLine(line);
Environment.Exit(result.Passed ? 0 : 1);
