using FluentAssertions;
using Lego2STL.Core.Pipeline;
using Lego2STL.Core.Rebrickable;
using Lego2STL.Core.Run;
using Lego2STL.Core.Text;
using Lego2STL.Tests.Run;

namespace Lego2STL.Tests.Pipeline;

/// <summary>
/// What the report says about the parts a run deliberately did not build.
/// </summary>
/// <remarks>
/// The report is the page someone prints from, so a part missing from the plates has to be
/// accounted for on it - otherwise the plates read as the whole set, which is the same mistake
/// the missing-parts line already exists to prevent.
/// </remarks>
public sealed class NotPrintedPartsTests
{
    private static RunOutcome AnOutcomeLeavingAHoseOut(RunLayout layout) =>
        APretendRun.Complete(layout) with
        {
            PartFacts = new Dictionary<string, PartFact>(StringComparer.OrdinalIgnoreCase)
            {
                ["5102c13"] = new("Tubes and Hoses", "Rubber"),
                ["22127"] = new("Electronics", "Plastic"),
            },
            NotPrinted = ["5102c13", "22127"],
        };

    [Fact]
    public async Task The_report_names_every_part_it_did_not_build()
    {
        var layout = RunLayout.At(APretendRun.TempFolder("notprinted"));

        await RunReport.WriteAsync(layout, AnOutcomeLeavingAHoseOut(layout), CancellationToken.None);
        var report = await File.ReadAllTextAsync(layout.ReportPath);

        report.Should().Contain("5102c13").And.Contain("22127");
    }

    /// <summary>The material is named, because "rubber" is what makes the answer obvious.</summary>
    [Fact]
    public async Task The_report_says_what_ruled_each_one_out()
    {
        var layout = RunLayout.At(APretendRun.TempFolder("notprinted-why"));

        await RunReport.WriteAsync(layout, AnOutcomeLeavingAHoseOut(layout), CancellationToken.None);
        var report = await File.ReadAllTextAsync(layout.ReportPath);

        report.Should().Contain("Rubber", "the material is the answer for the hose");
    }

    /// <summary>A run that left nothing out gains no section, and no blank heading either.</summary>
    [Fact]
    public async Task A_run_that_built_everything_says_nothing_about_it()
    {
        var layout = RunLayout.At(APretendRun.TempFolder("notprinted-none"));

        await RunReport.WriteAsync(layout, APretendRun.Complete(layout), CancellationToken.None);
        var report = await File.ReadAllTextAsync(layout.ReportPath);

        report.Should().NotContain(Strings.For(DisplayLanguages.Fallback)[TextKey.ReportNotPrintedTitle]);
    }
}
