using FluentAssertions;
using Lego2STL.Core.Colors;
using Lego2STL.Core.Pipeline;
using Lego2STL.Core.Text;

namespace Lego2STL.Tests.Pipeline;

/// <summary>
/// The settings both front ends share, and the command line the window shows for them.
/// </summary>
/// <remarks>
/// The shown command is a promise: run this and you get what is on screen. These tests hold
/// the tool to it, because a command that is nearly right is worse than none at all.
/// </remarks>
public sealed class RunSettingsTests
{
    [Fact]
    public void The_plainest_run_shows_the_plainest_command()
    {
        new RunSettings { Kind = InputKind.Document, InputPath = "model.pdf", Pages = "2-5" }
            .ToCommandLine()
            .Should().Be("lego2stl extract model.pdf 2-5");
    }

    [Fact]
    public void A_parts_list_becomes_a_build()
    {
        new RunSettings { Kind = InputKind.PartsList, InputPath = "parts.csv" }
            .ToCommandLine()
            .Should().Be("lego2stl build parts.csv");
    }

    [Fact]
    public void A_set_number_becomes_a_build_with_the_set()
    {
        new RunSettings { Kind = InputKind.SetNumber, SetNumber = "42100-1" }
            .ToCommandLine()
            .Should().Be("lego2stl build --set 42100-1");
    }

    /// <summary>
    /// Only what was changed appears. A command line repeating every default would be
    /// unreadable, and would teach the wrong lesson about what has to be typed.
    /// </summary>
    [Fact]
    public void Settings_left_at_their_default_are_not_written_out()
    {
        new RunSettings
        {
            Kind = InputKind.PartsList,
            InputPath = "parts.csv",
            ScalePercent = 100.0,
            PlateSpacing = 3.0,
            Printer = "A1",
            ColorScheme = ColorScheme.BrickLink,
            IncludeUnofficial = true,
        }
            .ToCommandLine()
            .Should().Be("lego2stl build parts.csv");
    }

    [Fact]
    public void Everything_changed_appears_exactly_once()
    {
        var command = new RunSettings
        {
            Kind = InputKind.PartsList,
            InputPath = "parts.csv",
            Clearance = 0.15,
            FillGaps = true,
            ScalePercent = 105,
            KeepOrigin = true,
            AsciiStl = true,
            NoSeamRepair = true,
            Offline = true,
            IncludeUnofficial = false,
            Printer = "A1mini",
            PlateSpacing = 5,
            Language = DisplayLanguage.Italian,
            Quiet = true,
        }.ToCommandLine();

        command.Should().Contain("--clearance 0.15");
        command.Should().Contain("--repair");
        command.Should().Contain("--scale 105");
        command.Should().Contain("--keep-origin");
        command.Should().Contain("--ascii");
        command.Should().Contain("--no-seam-repair");
        command.Should().Contain("--offline");
        command.Should().Contain("--no-unofficial");
        command.Should().Contain("--printer A1mini");
        command.Should().Contain("--plate-spacing 5");
        command.Should().Contain("--lang it");
        command.Should().Contain("--quiet");
    }

    [Fact]
    public void A_path_with_a_space_in_it_is_quoted()
    {
        new RunSettings { Kind = InputKind.PartsList, InputPath = @"C:\My Sets\parts.csv" }
            .ToCommandLine()
            .Should().Be("lego2stl build \"C:\\My Sets\\parts.csv\"");
    }

    /// <summary>
    /// The point of showing the command is that it can be pasted somewhere. A key pasted
    /// along with it is a key in a chat window, so it is named and not printed.
    /// </summary>
    [Fact]
    public void An_api_key_is_never_written_into_the_shown_command()
    {
        new RunSettings { Kind = InputKind.SetNumber, SetNumber = "42100-1", ApiKey = "secret-value" }
            .ToCommandLine()
            .Should().NotContain("secret-value").And.Contain("--api-key <your key>");
    }

    [Fact]
    public void A_bed_size_replaces_the_printer_rather_than_joining_it()
    {
        var command = new RunSettings
        {
            Kind = InputKind.PartsList,
            InputPath = "parts.csv",
            Printer = "X1C",
            PlateSize = "220x220",
        }.ToCommandLine();

        command.Should().Contain("--plate-size 220x220");
        command.Should().NotContain("--printer");
    }

    [Theory]
    [InlineData(RunStages.PartsListOnly, "--csv-only")]
    [InlineData(RunStages.Shapes, "--no-plates")]
    public void How_far_to_go_is_said_when_it_is_not_all_the_way(RunStages stages, string expected)
    {
        new RunSettings { Kind = InputKind.PartsList, InputPath = "parts.csv", Stages = stages }
            .ToCommandLine()
            .Should().Contain(expected);
    }

    [Fact]
    public void Going_all_the_way_says_nothing_about_stages()
    {
        var command = new RunSettings
        {
            Kind = InputKind.PartsList,
            InputPath = "parts.csv",
            Stages = RunStages.ShapesAndPlates,
        }.ToCommandLine();

        command.Should().NotContain("--csv-only").And.NotContain("--no-plates");
    }

    [Fact]
    public void A_tab_separator_is_named_rather_than_written()
    {
        new RunSettings { Kind = InputKind.PartsList, InputPath = "parts.csv", Delimiter = '\t' }
            .ToCommandLine()
            .Should().Contain("--delimiter tab");
    }

    // ---- What the settings refuse before anything runs -----------------------------------

    [Fact]
    public void A_run_with_nothing_chosen_says_what_is_missing()
    {
        new RunSettings { Kind = InputKind.Document, InputPath = null }
            .Problems()
            .Should().ContainSingle().Which.Should().Contain("Choose a document");
    }

    [Fact]
    public void A_set_run_with_no_number_says_so()
    {
        new RunSettings { Kind = InputKind.SetNumber }
            .Problems()
            .Should().ContainSingle().Which.Should().Contain("set number");
    }

    [Fact]
    public void A_negative_clearance_is_refused_before_anything_is_written()
    {
        new RunSettings { Kind = InputKind.SetNumber, SetNumber = "42100-1", Clearance = -0.1 }
            .Problems()
            .Should().ContainSingle().Which.Should().Contain("cannot be negative");
    }

    [Fact]
    public void A_bed_size_that_is_not_a_size_is_refused()
    {
        new RunSettings { Kind = InputKind.SetNumber, SetNumber = "42100-1", PlateSize = "enormous" }
            .Problems()
            .Should().ContainSingle().Which.Should().Contain("not a bed size");
    }

    [Fact]
    public void A_run_that_is_ready_has_nothing_to_say()
    {
        new RunSettings { Kind = InputKind.SetNumber, SetNumber = "42100-1" }
            .Problems()
            .Should().BeEmpty();
    }

    // ---- What the settings hand to the stages --------------------------------------------

    [Fact]
    public void The_stages_asked_for_reach_the_geometry_and_plate_settings()
    {
        var settings = new RunSettings
        {
            Clearance = 0.2,
            FillGaps = true,
            KeepOrigin = true,
            NoSeamRepair = true,
            ScalePercent = 90,
            PlateSpacing = 4,
            Printer = "H2D",
        };

        settings.MeshOptions.ClearanceMillimetres.Should().BeApproximately(0.2f, 1e-6f);
        settings.MeshOptions.FillGaps.Should().BeTrue();
        settings.MeshOptions.PlaceOnBed.Should().BeFalse();
        settings.MeshOptions.RepairSeams.Should().BeFalse();
        settings.MeshOptions.ScalePercent.Should().Be(90f);

        settings.PackingOptions.Spacing.Should().Be(4f);
        settings.PackingOptions.Bed.Name.Should().Be("H2D");
    }
}
