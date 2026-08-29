using System.Text.Json.Serialization;

namespace Lego2STL.Core.Rebrickable;

/// <summary>A page of results from any Rebrickable list endpoint.</summary>
internal sealed class RbPage<T>
{
    [JsonPropertyName("count")] public int Count { get; set; }

    [JsonPropertyName("next")] public string? Next { get; set; }

    [JsonPropertyName("results")] public List<T> Results { get; set; } = [];
}

internal sealed class RbColor
{
    [JsonPropertyName("id")] public int Id { get; set; }

    [JsonPropertyName("name")] public string Name { get; set; } = "";

    [JsonPropertyName("rgb")] public string Rgb { get; set; } = "";

    [JsonPropertyName("is_trans")] public bool IsTrans { get; set; }

    /// <summary>
    /// Keyed by catalogue name: "BrickLink", "LEGO", "LDraw", "BrickOwl", "Peeron".
    /// Note that <see cref="RbExternalIds.ExtIds"/> is an array: black carries LEGO
    /// [26, 342] and LDraw [0, 256], so the first entry is the primary and later ones
    /// are aliases.
    /// </summary>
    [JsonPropertyName("external_ids")]
    public Dictionary<string, RbExternalIds>? ExternalIds { get; set; }
}

internal sealed class RbExternalIds
{
    /// <summary>
    /// Nullable elements are not defensive padding: Peeron really does return
    /// <c>"ext_ids": [null]</c> for some colours, which a List&lt;int&gt; cannot hold.
    /// </summary>
    [JsonPropertyName("ext_ids")] public List<int?>? ExtIds { get; set; }

    /// <summary>One list of names per entry in <see cref="ExtIds"/>.</summary>
    [JsonPropertyName("ext_descrs")] public List<List<string>?>? ExtDescrs { get; set; }
}

internal sealed class RbPart
{
    [JsonPropertyName("part_num")] public string PartNum { get; set; } = "";

    [JsonPropertyName("name")] public string Name { get; set; } = "";

    [JsonPropertyName("part_cat_id")] public int? PartCatId { get; set; }

    [JsonPropertyName("part_img_url")] public string? PartImgUrl { get; set; }

    [JsonPropertyName("external_ids")]
    public Dictionary<string, List<string>>? ExternalIds { get; set; }
}

/// <summary>One element number, resolved: the moulding it names and the colour it is in.</summary>
internal sealed class RbElement
{
    [JsonPropertyName("element_id")] public string? ElementId { get; set; }

    [JsonPropertyName("part")] public RbPart? Part { get; set; }

    [JsonPropertyName("color")] public RbColor? Color { get; set; }
}

internal sealed class RbSetPart
{
    [JsonPropertyName("quantity")] public int Quantity { get; set; }

    [JsonPropertyName("is_spare")] public bool IsSpare { get; set; }

    [JsonPropertyName("element_id")] public string? ElementId { get; set; }

    [JsonPropertyName("part")] public RbPart? Part { get; set; }

    [JsonPropertyName("color")] public RbColor? Color { get; set; }
}
