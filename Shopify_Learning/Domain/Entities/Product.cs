namespace ShopifyIntegration.Domain.Entities;

public sealed class Product
{
    public int            Id         { get; set; }       // PK, auto-increment
    public string         ShopifyGid { get; set; } = ""; // unique, not null
    public long           NumericId  { get; set; }       // not null
    public string         Title      { get; set; } = ""; // not null
    public string         Vendor     { get; set; } = ""; // not null
    public string         Status     { get; set; } = ""; // not null
    public DateTimeOffset CreatedAt  { get; set; }       // timestamptz, not null
    public DateTimeOffset UpdatedAt  { get; set; }       // timestamptz, not null
}
