using System.Text.Json;
using FluentAssertions;
using Lego2STL.Core.Plates;

namespace Lego2STL.Tests.Plates;

/// <summary>
/// The slicer settings that travel beside a plate.
/// </summary>
/// <remarks>
/// Thin on purpose. A preset that inherits from the printer's own base and states only the
/// differences stays small enough to read and cannot contradict the base underneath it; a copied
/// profile would go stale at the next release of the slicer.
/// </remarks>
public sealed class ProcessPresetTests
{
    private static JsonElement Parsed(string printer) =>
        JsonDocument.Parse(ProcessPreset.For(printer)!).RootElement;

    [Theory]
    [InlineData("A1", "0.16mm Optimal @BBL A1")]
    [InlineData("A1mini", "0.16mm Optimal @BBL A1M")]
    [InlineData("P1P", "0.16mm Optimal @BBL P1P")]
    [InlineData("X1C", "0.16mm Optimal @BBL X1C")]
    [InlineData("H2D", "0.16mm Standard @BBL H2D")]
    public void Each_printer_inherits_from_its_own_base(string printer, string expected) =>
        Parsed(printer).GetProperty("inherits").GetString().Should().Be(expected);

    /// <summary>
    /// The P1S has no profiles of its own and uses the P1P's.
    /// </summary>
    /// <remarks>
    /// A substitution rather than a match, which is why the instruction sheet names it as one.
    /// </remarks>
    [Fact]
    public void The_P1S_borrows_the_P1P_profile()
    {
        Parsed("P1S").GetProperty("inherits").GetString().Should().Be("0.16mm Optimal @BBL P1P");
        ProcessPreset.BorrowedFrom("P1S").Should().Be("P1P");
    }

    /// <summary>
    /// The A1 mini has profiles of its own; they are simply named differently.
    /// </summary>
    /// <remarks>
    /// The case that rules out inferring a borrowing from whether the base mentions the printer:
    /// the A1 mini's own base is named A1M, and calling that a substitution would be a lie on the
    /// sheet.
    /// </remarks>
    [Fact]
    public void The_A1_mini_borrows_nothing() =>
        ProcessPreset.BorrowedFrom("A1mini").Should().BeNull();

    [Fact]
    public void A_printer_with_no_known_base_gets_no_file() =>
        ProcessPreset.For("some future machine").Should().BeNull();

    /// <summary>Every value is a string, and the speeds are arrays of them. That is the format.</summary>
    [Fact]
    public void Every_value_is_written_the_way_the_slicer_writes_them()
    {
        var preset = Parsed("A1");

        preset.GetProperty("type").GetString().Should().Be("process");
        preset.GetProperty("from").GetString().Should().Be("User");
        preset.GetProperty("elefant_foot_compensation").ValueKind.Should().Be(JsonValueKind.String);
        preset.GetProperty("outer_wall_speed").ValueKind.Should().Be(JsonValueKind.Array);
        preset.GetProperty("outer_wall_speed")[0].ValueKind.Should().Be(JsonValueKind.String);
    }

    /// <summary>
    /// A per-extruder setting carries one value per slot the base carries, which is not always one.
    /// </summary>
    /// <remarks>
    /// Read off the bases themselves on 2026-08-31, by resolving each inheritance chain down to
    /// fdm_process_common: the A1 and A1 mini carry one, the P1P and X1C carry two, and the H2D
    /// carries seven. So "an array of one on a single-extruder machine" is wrong - the P1P and the
    /// X1C are single-extruder machines whose profiles list two - and a preset that writes one
    /// value where the base has seven hands the slicer a vector of the wrong length.
    /// </remarks>
    [Theory]
    [InlineData("A1", 1)]
    [InlineData("A1mini", 1)]
    [InlineData("P1P", 2)]
    [InlineData("P1S", 2)]
    [InlineData("X1C", 2)]
    [InlineData("H2D", 7)]
    public void A_speed_carries_one_value_for_every_slot_its_base_carries(string printer, int slots)
    {
        var preset = Parsed(printer);

        preset.GetProperty("outer_wall_speed").GetArrayLength().Should().Be(slots);
        preset.GetProperty("small_perimeter_speed").GetArrayLength().Should().Be(slots);
    }

    /// <summary>
    /// A small perimeter is stated as a share of the outer wall, because that is the only form
    /// the slicer's own profiles ever use for it.
    /// </summary>
    /// <remarks>
    /// Every small_perimeter_speed in every profile Bambu Studio ships, across all ten vendors, is
    /// a percentage. An absolute figure is probably accepted too, but "probably accepted" is what
    /// this whole verification step exists to refuse.
    /// </remarks>
    [Fact]
    public void A_small_perimeter_is_a_share_of_the_outer_wall() =>
        Parsed("A1").GetProperty("small_perimeter_speed")[0].GetString().Should().EndWith("%");

    /// <summary>
    /// The layer height is not asserted, because the base already is one.
    /// </summary>
    /// <remarks>
    /// Choosing a 0.16 mm base and then also writing 0.16 mm would be two places to keep in step,
    /// and the second one would eventually be the wrong one.
    /// </remarks>
    [Fact]
    public void The_layer_height_is_inherited_rather_than_repeated() =>
        Parsed("A1").TryGetProperty("layer_height", out _).Should().BeFalse();

    /// <summary>Nothing about the spool, because the tool has never seen the spool.</summary>
    [Theory]
    [InlineData("nozzle_temperature")]
    [InlineData("hot_plate_temp")]
    [InlineData("filament_max_volumetric_speed")]
    public void Nothing_about_the_filament_is_asserted(string key) =>
        Parsed("A1").TryGetProperty(key, out _).Should().BeFalse();
}
