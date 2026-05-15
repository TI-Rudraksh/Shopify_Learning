namespace ShopifyIntegration.Domain.Entities;

public sealed class OrderLineItem
{
    public int    Id           { get; set; }       // PK, auto-increment
    public int    OrderId      { get; set; }       // FK → Order.Id, not null
    public Order  Order        { get; set; } = null!; // navigation property
    public string ShopifyGid   { get; set; } = ""; // not null
    public long   NumericId    { get; set; }       // not null
    public string Title        { get; set; } = ""; // not null
    public string VariantTitle { get; set; } = ""; // not null
    public int    Quantity     { get; set; }       // not null
    public decimal Price       { get; set; }       // not null
    public string Sku          { get; set; } = ""; // not null
    public string ProductGid   { get; set; } = ""; // not null
    public string VariantGid   { get; set; } = ""; // not null
}
