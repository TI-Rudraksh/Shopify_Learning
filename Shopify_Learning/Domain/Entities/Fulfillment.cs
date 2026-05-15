namespace ShopifyIntegration.Domain.Entities;

public sealed class Fulfillment
{
    public int            Id                  { get; set; }       // PK, auto-increment
    public string         ShopifyGid          { get; set; } = ""; // unique, not null
    public long           NumericId           { get; set; }       // not null
    public int            OrderId             { get; set; }       // FK → Order.Id, not null
    public Order          Order               { get; set; } = null!; // navigation property
    public string         Status              { get; set; } = ""; // not null
    public string?        TrackingNumber      { get; set; }       // nullable
    public string?        TrackingCompany     { get; set; }       // nullable
    public string?        TrackingUrl         { get; set; }       // nullable
    public string?        FulfillmentOrderGid { get; set; }       // nullable
    public DateTimeOffset CreatedAt           { get; set; }       // timestamptz, not null
    public DateTimeOffset UpdatedAt           { get; set; }       // timestamptz, not null
}
