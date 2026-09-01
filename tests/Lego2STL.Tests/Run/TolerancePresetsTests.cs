using FluentAssertions;
using Lego2STL.Core.Run;

namespace Lego2STL.Tests.Run;

/// <summary>
/// The clearance you measured, kept under a name you chose.
/// </summary>
/// <remarks>
/// A name rather than a composed key of printer and nozzle and material, because two spools of
/// the same material behave differently and a machine drifts. The name is what survives that; a
/// key is not.
/// </remarks>
[Collection(AppDataFolder.Name)]
public sealed class TolerancePresetsTests : IDisposable
{
    private readonly AppDataFolder _folder = new();

    public void Dispose() => _folder.Dispose();

    [Fact]
    public void A_saved_figure_comes_back()
    {
        TolerancePresets.Remember("eSUN PLA+ black - A1", 0.15, preferred: false);

        var read = TolerancePresets.Load();

        read.Should().ContainSingle();
        read[0].Name.Should().Be("eSUN PLA+ black - A1");
        read[0].Millimetres.Should().Be(0.15);
        read[0].Preferred.Should().BeFalse();
    }

    /// <summary>Losing a preference must never stop a run, so an unreadable file is no presets.</summary>
    [Fact]
    public void A_file_that_cannot_be_read_is_treated_as_none()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(TolerancePresets.FilePath)!);
        File.WriteAllText(TolerancePresets.FilePath, "{ this is not json");

        TolerancePresets.Load().Should().BeEmpty();
    }

    [Fact]
    public void No_file_at_all_is_no_presets() => TolerancePresets.Load().Should().BeEmpty();

    /// <summary>Saving a name that exists replaces it rather than making a second of it.</summary>
    [Fact]
    public void Saving_a_name_that_exists_replaces_it()
    {
        TolerancePresets.Remember("black", 0.10, preferred: false);
        TolerancePresets.Remember("black", 0.20, preferred: false);

        var read = TolerancePresets.Load();

        read.Should().ContainSingle();
        read[0].Millimetres.Should().Be(0.20);
    }

    /// <summary>
    /// At most one preset is preferred, and that is the store's own guarantee.
    /// </summary>
    /// <remarks>
    /// An invariant here rather than a convention its callers keep, because a build silently
    /// picking one of two preferred presets would apply a number nobody chose.
    /// </remarks>
    [Fact]
    public void Only_one_preset_is_ever_preferred()
    {
        TolerancePresets.Remember("first", 0.10, preferred: true);
        TolerancePresets.Remember("second", 0.20, preferred: true);

        var read = TolerancePresets.Load();

        read.Should().HaveCount(2);
        read.Where(p => p.Preferred).Should().ContainSingle(p => p.Name == "second");
    }

    [Fact]
    public void Preferring_one_unprefers_the_others()
    {
        TolerancePresets.Remember("first", 0.10, preferred: true);
        TolerancePresets.Remember("second", 0.20, preferred: false);

        TolerancePresets.Prefer("second");

        TolerancePresets.Load().Where(p => p.Preferred).Should().ContainSingle(p => p.Name == "second");
    }

    [Fact]
    public void Forgetting_one_leaves_the_rest()
    {
        TolerancePresets.Remember("first", 0.10, preferred: false);
        TolerancePresets.Remember("second", 0.20, preferred: false);

        TolerancePresets.Forget("first");

        TolerancePresets.Load().Should().ContainSingle(p => p.Name == "second");
    }

    /// <summary>A name is matched the way a person would match it, ignoring case and edges.</summary>
    [Fact]
    public void A_name_is_found_however_it_is_typed()
    {
        TolerancePresets.Remember("eSUN PLA+ black - A1", 0.15, preferred: false);

        TolerancePresets.Find(TolerancePresets.Load(), "  esun pla+ BLACK - a1 ")
            .Should().NotBeNull();
    }
}
