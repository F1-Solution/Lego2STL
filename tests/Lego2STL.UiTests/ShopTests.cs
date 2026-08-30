using FluentAssertions;
using Lego2STL.Gui.Services;

namespace Lego2STL.UiTests;

/// <summary>
/// Turning a part into an address at a shop.
/// </summary>
/// <remarks>
/// The rule that matters is the one about element numbers: a shop that sells by element number
/// cannot be given a part read from a CSV, which has none, and answering with an address that
/// leads nowhere is worse than answering with a search.
/// </remarks>
public sealed class ShopTests
{
    private static readonly Shop ByPart = new("A shop", "https://shop/part/{part}", "https://shop/find?q={part}");
    private static readonly Shop ByElement = new("Another", "https://other/{element}", "https://other/find?q={part}");

    [Fact]
    public void A_part_number_goes_where_the_template_says()
    {
        Shops.AddressOf(ByPart, "32523", elementId: null, colorCode: 11)
            .Should().Be("https://shop/part/32523");
    }

    [Fact]
    public void An_element_number_is_used_when_the_shop_asks_for_one()
    {
        Shops.AddressOf(ByElement, "32523", "6177114", 11).Should().Be("https://other/6177114");
    }

    /// <summary>A list from a CSV has no element numbers, so the shop's search is used instead.</summary>
    [Fact]
    public void A_shop_that_needs_an_element_number_falls_back_to_its_search()
    {
        Shops.AddressOf(ByElement, "32523", elementId: null, colorCode: 11)
            .Should().Be("https://other/find?q=32523");
    }

    [Fact]
    public void A_shop_that_needs_one_and_has_no_search_has_no_address()
    {
        var awkward = new Shop("Awkward", "https://awkward/{element}", Search: null);

        Shops.AddressOf(awkward, "32523", elementId: null, colorCode: 11).Should().BeNull();
    }

    [Fact]
    public void A_colour_code_is_substituted_when_the_template_wants_one()
    {
        var byColour = new Shop("Colourful", "https://c/{part}?colour={color}", null);

        Shops.AddressOf(byColour, "32523", null, 11).Should().Be("https://c/32523?colour=11");
    }

    /// <summary>A part number goes into an address, so it has to be escaped like one.</summary>
    [Fact]
    public void A_part_number_with_awkward_characters_is_escaped()
    {
        Shops.AddressOf(ByPart, "3 4&5", null, 11).Should().Be("https://shop/part/3%204%265");
    }

    [Fact]
    public void The_three_shops_offered_at_first_all_produce_an_address()
    {
        Shops.Defaults.Should().HaveCount(3);

        foreach (var shop in Shops.Defaults)
        {
            Shops.AddressOf(shop, "32523", "6177114", 11).Should().NotBeNullOrWhiteSpace();
        }
    }

    [Fact]
    public void The_shops_survive_being_written_and_read_back()
    {
        var settings = new UserSettings
        {
            Shops = [new Shop("Mine", "https://mine/{part}", null)],
            PreferredShop = "Mine",
        };

        var json = System.Text.Json.JsonSerializer.Serialize(settings);
        var read = System.Text.Json.JsonSerializer.Deserialize<UserSettings>(json)!;

        read.Shops.Should().ContainSingle().Which.Url.Should().Be("https://mine/{part}");
        read.PreferredShop.Should().Be("Mine");
    }
}
