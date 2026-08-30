using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json.Serialization;

namespace Lego2STL.Gui.Services;

/// <summary>
/// Somewhere a part can be bought, and how to build the address of one there.
/// </summary>
/// <param name="Url">
/// The address of a part's own page, with <c>{part}</c>, <c>{element}</c> and <c>{color}</c>
/// standing for what is known about it.
/// </param>
/// <param name="Search">
/// Where to search, for a part this shop's own page cannot be built for. Optional.
/// </param>
public sealed record Shop(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("url")] string Url,
    [property: JsonPropertyName("search")] string? Search);

/// <summary>The shops offered, and the addresses they lead to.</summary>
public static class Shops
{
    /// <summary>What the settings start with, and what a cleared list goes back to.</summary>
    public static IReadOnlyList<Shop> Defaults { get; } =
    [
        new("BrickLink",
            "https://www.bricklink.com/v2/catalog/catalogitem.page?P={part}",
            "https://www.bricklink.com/v2/search.page?q={part}"),
        new("Rebrickable",
            "https://rebrickable.com/parts/{part}/",
            "https://rebrickable.com/search/?q={part}"),
        new("LEGO Pick a Brick",
            "https://www.lego.com/pick-and-build/pick-a-brick?query={element}",
            "https://www.lego.com/pick-and-build/pick-a-brick?query={part}"),
    ];

    /// <summary>
    /// Where this shop sells this part, or null when it cannot be said.
    /// </summary>
    /// <remarks>
    /// A shop that sells by element number is no use to a list that has none - a list read from
    /// a CSV or from a set number - so its search is used instead, and when it has no search
    /// there is no honest address to give.
    /// </remarks>
    public static string? AddressOf(Shop shop, string partNumber, string? elementId, int colorCode)
    {
        ArgumentNullException.ThrowIfNull(shop);

        var wantsElement = shop.Url.Contains("{element}", StringComparison.Ordinal);
        var template = wantsElement && string.IsNullOrWhiteSpace(elementId) ? shop.Search : shop.Url;

        if (string.IsNullOrWhiteSpace(template))
        {
            return null;
        }

        if (template.Contains("{element}", StringComparison.Ordinal) &&
            string.IsNullOrWhiteSpace(elementId))
        {
            return null;
        }

        return template
            .Replace("{part}", Uri.EscapeDataString(partNumber ?? string.Empty), StringComparison.Ordinal)
            .Replace("{element}", Uri.EscapeDataString(elementId ?? string.Empty), StringComparison.Ordinal)
            .Replace("{color}", colorCode.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);
    }
}
