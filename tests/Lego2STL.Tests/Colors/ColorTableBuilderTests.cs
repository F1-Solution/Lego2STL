using FluentAssertions;
using Lego2STL.Core.Colors;
using Lego2STL.Core.Rebrickable;

namespace Lego2STL.Tests.Colors;

/// <summary>
/// Exercises the reverse-map resolution rules against synthetic catalogues that reproduce
/// the collisions present in the live Rebrickable data, without touching the network.
/// </summary>
public sealed class ColorTableBuilderTests
{
    [Fact]
    public void A_code_claimed_by_one_colour_resolves_to_it()
    {
        var result = Build(Color(0, "Black", "05131D", brickLink: [11]));

        result.Table.Get(ColorScheme.BrickLink, 11).Name.Should().Be("Black");
        result.Notes.Should().BeEmpty("nothing was contested");
    }

    /// <summary>
    /// The LDraw 112 case: Medium Bluish Violet lists 112 first, Medium Violet lists it
    /// second behind 219. Being primary wins.
    /// </summary>
    [Fact]
    public void A_colour_whose_primary_code_it_is_beats_one_that_only_aliases_it()
    {
        var result = Build(
            Color(112, "Medium Bluish Violet", "6874CA", lDraw: [112]),
            Color(1001, "Medium Violet", "9391E4", lDraw: [219, 112]));

        result.Table.Get(ColorScheme.LDraw, 112).Name.Should().Be("Medium Bluish Violet");
        result.Table.Get(ColorScheme.LDraw, 219).Name.Should().Be("Medium Violet");
        result.Notes.Should().ContainSingle().Which.Should().Contain("primary id");
    }

    /// <summary>
    /// The BrickLink 77 case: both colours list 77 as their primary, but BrickLink itself
    /// calls 77 "Pearl Dark Gray", which is one colour's name and not the other's.
    /// </summary>
    [Fact]
    public void When_both_claim_it_primarily_the_colour_matching_the_catalogue_name_wins()
    {
        var result = Build(
            Color(148, "Pearl Dark Gray", "575857", brickLink: [77], brickLinkNames: ["Pearl Dark Gray"]),
            Color(1103, "Pearl Titanium", "3E3C39", brickLink: [77], brickLinkNames: ["Pearl Dark Gray"]));

        result.Table.Get(ColorScheme.BrickLink, 77).Name.Should().Be("Pearl Dark Gray");
        result.Notes.Should().ContainSingle().Which.Should().Contain("matches the catalogue's own name");
    }

    /// <summary>LDraw writes names with underscores, so the comparison has to be loose.</summary>
    [Fact]
    public void Catalogue_name_matching_ignores_underscores_and_case()
    {
        var result = Build(
            Color(216, "Rust", "B31004", lDraw: [216], lDrawNames: ["Rust"]),
            Color(1081, "Rust Orange", "872B17", lDraw: [216], lDrawNames: ["RUST"]));

        result.Table.Get(ColorScheme.LDraw, 216).Name.Should().Be("Rust");
    }

    [Fact]
    public void With_no_name_match_the_colour_with_more_parts_wins()
    {
        var result = Build(
            new[]
            {
                Color(76, "Speckle DBGray-Silver", "6C6E68", lego: [304]),
                Color(132, "Speckle Black-Silver", "05131D", lego: [304]),
            },
            partCounts: new Dictionary<int, int> { [76] = 12, [132] = 246 });

        result.Table.Get(ColorScheme.Lego, 304).Name.Should().Be("Speckle Black-Silver");
        result.Notes.Should().ContainSingle().Which.Should().Contain("more parts");
    }

    [Fact]
    public void With_everything_tied_the_lowest_Rebrickable_id_wins_so_the_result_is_deterministic()
    {
        var result = Build(
            Color(500, "Alpha", "010101", brickLink: [900]),
            Color(400, "Beta", "020202", brickLink: [900]));

        result.Table.Get(ColorScheme.BrickLink, 900).Name.Should().Be("Beta");
        result.Notes.Should().ContainSingle().Which.Should().Contain("deterministic");
    }

    /// <summary>
    /// The sentinel claims LDraw's meta-colours 16 and 24. Letting it win would make
    /// "what colour is LDraw 16?" answer "[Unknown]", which is worse than not answering.
    /// </summary>
    [Fact]
    public void The_unknown_sentinel_never_wins_an_external_code()
    {
        var result = Build(
            Color(LegoColor.UnknownRebrickableId, "[Unknown]", "0033B2", lDraw: [16, 24]),
            Color(0, "Black", "05131D", lDraw: [0]));

        result.Table.TryGet(ColorScheme.LDraw, 16, out _).Should().BeFalse();
        result.Table.TryGet(ColorScheme.LDraw, 24, out _).Should().BeFalse();
        result.Table.Get(ColorScheme.LDraw, 0).Name.Should().Be("Black");
    }

    [Fact]
    public void The_unknown_sentinel_is_still_reachable_by_its_own_Rebrickable_id()
    {
        var result = Build(Color(LegoColor.UnknownRebrickableId, "[Unknown]", "0033B2"));

        result.Table.Get(ColorScheme.Rebrickable, LegoColor.UnknownRebrickableId)
            .IsUnknown.Should().BeTrue();
    }

    /// <summary>Peeron returns "ext_ids": [null] for some colours; it must not throw.</summary>
    [Fact]
    public void Null_external_ids_are_skipped_without_breaking_alignment()
    {
        var raw = new RbColor
        {
            Id = 0,
            Name = "Black",
            Rgb = "05131D",
            ExternalIds = new Dictionary<string, RbExternalIds>
            {
                ["LDraw"] = new() { ExtIds = [null, 0], ExtDescrs = [["ignored"], ["Black"]] },
            },
        };

        var result = ColorTableBuilder.Build([raw], partCounts: null);

        result.Table.Get(ColorScheme.LDraw, 0).Name.Should().Be("Black");
        result.Colors.Single().LDrawId.Should().Be(0, "the null entry must not become the primary");
    }

    [Fact]
    public void An_unreadable_rgb_is_reported_rather_than_crashing()
    {
        var result = Build(Color(0, "Broken", "not-a-colour"));

        result.Colors.Single().Rgb.Should().Be(new Rgb24(0, 0, 0));
        result.Notes.Should().ContainSingle().Which.Should().Contain("unreadable RGB");
    }

    [Fact]
    public void Rebrickable_ids_always_map_to_themselves()
    {
        var result = Build(
            Color(0, "Black", "05131D"),
            Color(71, "Light Bluish Gray", "A0A5A9"));

        result.Table.Get(ColorScheme.Rebrickable, 0).Name.Should().Be("Black");
        result.Table.Get(ColorScheme.Rebrickable, 71).Name.Should().Be("Light Bluish Gray");
    }

    [Fact]
    public void Colours_without_an_external_code_are_absent_from_that_scheme()
    {
        var result = Build(Color(9999, "Made Up", "123456"));

        result.Table.CountIn(ColorScheme.BrickLink).Should().Be(0);
        result.Colors.Single().BrickLinkId.Should().BeNull();
    }

    private static ColorTableBuildResult Build(params RbColor[] colors) =>
        ColorTableBuilder.Build(colors, partCounts: null);

    private static ColorTableBuildResult Build(RbColor[] colors, IReadOnlyDictionary<int, int> partCounts) =>
        ColorTableBuilder.Build(colors, partCounts);

    private static RbColor Color(
        int id,
        string name,
        string rgb,
        int?[]? brickLink = null,
        int?[]? lego = null,
        int?[]? lDraw = null,
        string[]? brickLinkNames = null,
        string[]? lDrawNames = null)
    {
        var external = new Dictionary<string, RbExternalIds>();
        Add("BrickLink", brickLink, brickLinkNames);
        Add("LEGO", lego, null);
        Add("LDraw", lDraw, lDrawNames);

        return new RbColor
        {
            Id = id,
            Name = name,
            Rgb = rgb,
            ExternalIds = external.Count > 0 ? external : null,
        };

        void Add(string key, int?[]? ids, string[]? names)
        {
            if (ids is null)
            {
                return;
            }

            external[key] = new RbExternalIds
            {
                ExtIds = [.. ids],
                ExtDescrs = names is null
                    ? null
                    : [.. names.Select(n => new List<string> { n })],
            };
        }
    }
}
