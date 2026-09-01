using System;
using System.CommandLine;
using System.IO;
using FluentAssertions;
using Lego2STL.Cli;
using Lego2STL.Cli.Commands;
using Lego2STL.Core.Catalogue;
using Lego2STL.Core.Pipeline;
using Lego2STL.Core.Run;
using Lego2STL.Core.Text;

namespace Lego2STL.UiTests;

/// <summary>
/// A saved tolerance reaching a build, through the real option declaration.
/// </summary>
/// <remarks>
/// Driven through PipelineOptions rather than through the resolver alone, because the wiring
/// between the two is the part that can be wrong without any test noticing: the resolver can be
/// perfect and simply never be called. In this project rather than beside the other pipeline
/// tests because this one is the only assembly the command line shows its internals to.
/// </remarks>
public sealed class TolerancesReachABuildTests : IDisposable
{
    /// <summary>This assembly shares one settings folder, so the store is emptied rather than moved.</summary>
    public TolerancesReachABuildTests() => Clear();

    public void Dispose() => Clear();

    private static void Clear()
    {
        if (File.Exists(TolerancePresets.FilePath))
        {
            File.Delete(TolerancePresets.FilePath);
        }
    }

    /// <summary>The real declaration, parsed and read exactly as the build command reads it.</summary>
    private static RunSettings ParseBuild(params string[] args)
    {
        var options = new PipelineOptions(Strings.English);
        var command = new Command("build");
        options.AddTo(command, includeDocumentOptions: false);
        command.Options.Add(CommonOptions.Language);

        return options.Read(command.Parse(args), InputKind.PartsList, "parts.csv", null);
    }

    [Fact]
    public void A_named_tolerance_supplies_the_clearance()
    {
        TolerancePresets.Remember("black", 0.15, preferred: false);

        var settings = ParseBuild("--tolerances", "black");

        settings.Clearance.Should().Be(0.15);
        settings.ClearanceFrom.Should().Be("black");
    }

    [Fact]
    public void A_preferred_tolerance_supplies_it_with_nothing_asked_for()
    {
        TolerancePresets.Remember("black", 0.15, preferred: true);

        var settings = ParseBuild();

        settings.Clearance.Should().Be(0.15);
        settings.ClearanceFrom.Should().Be("black");
    }

    /// <summary>The reason the option had to become nullable.</summary>
    [Fact]
    public void An_explicit_zero_turns_a_preferred_tolerance_off()
    {
        TolerancePresets.Remember("black", 0.15, preferred: true);

        var settings = ParseBuild("--clearance", "0");

        settings.Clearance.Should().Be(0);
        settings.ClearanceFrom.Should().BeNull();
    }

    /// <summary>The refusal to guess, still standing.</summary>
    [Fact]
    public void Nothing_saved_leaves_a_build_at_true_size()
    {
        var settings = ParseBuild();

        settings.Clearance.Should().Be(0);
        settings.ClearanceFrom.Should().BeNull();
    }

    /// <summary>An explicit figure still beats a saved one, which is the whole precedence rule.</summary>
    [Fact]
    public void An_explicit_clearance_still_wins()
    {
        TolerancePresets.Remember("black", 0.15, preferred: true);

        ParseBuild("--clearance", "0.05").Clearance.Should().Be(0.05);
    }
}
