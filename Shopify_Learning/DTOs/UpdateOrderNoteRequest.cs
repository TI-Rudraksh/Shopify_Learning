using System.Text.Json.Serialization;

namespace ShopifyIntegration.DTOs;

public sealed class UpdateOrderNoteRequest
{
    /// <summary>The order note text.</summary>
    [JsonPropertyName("note")]
    public string? Note { get; set; }

    /// <summary>Note attributes to append/update. Matches Shopify's note_attributes shape.</summary>
    [JsonPropertyName("note_attributes")]
    public List<NoteAttributeItem> NoteAttributes { get; set; } = new();
}

public sealed class NoteAttributeItem
{
    [JsonPropertyName("name")]
    public string Name  { get; set; } = "";

    [JsonPropertyName("value")]
    public string Value { get; set; } = "";
}
