namespace ShopifyIntegration.Domain.Entities;

public sealed class InventoryLevel
{
    public int            Id               { get; set; }       // PK, auto-increment
    public int            ProductId        { get; set; }       // FK → Product.Id, not null
    public Product        Product          { get; set; } = null!; // navigation property
    public string         LocationGid      { get; set; } = ""; // not null
    public string         InventoryItemGid { get; set; } = ""; // not null
    public int            Quantity         { get; set; }       // not null
    public bool           Available        { get; set; }       // not null
    public DateTimeOffset CreatedAt        { get; set; }       // timestamptz, not null
    public DateTimeOffset UpdatedAt        { get; set; }       // timestamptz, not null
    public uint           XMin             { get; set; }       // PostgreSQL xmin concurrency token
}
