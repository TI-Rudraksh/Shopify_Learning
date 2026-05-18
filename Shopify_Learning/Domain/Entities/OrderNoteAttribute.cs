namespace ShopifyIntegration.Domain.Entities;

public sealed class OrderNoteAttribute
{
    public int    Id      { get; set; }       // PK, auto-increment
    public int    OrderId { get; set; }       // FK → Order.Id, not null
    public Order  Order   { get; set; } = null!; // navigation property
    public string Name    { get; set; } = ""; // attribute key, not null
    public string Value   { get; set; } = ""; // attribute value, not null
}
