namespace ShopifyIntegration.Domain.Entities;

public sealed class Location
{
    public int            Id          { get; set; }       // PK, auto-increment
    public string         LocationGid { get; set; } = ""; // unique, not null
    public long           NumericId   { get; set; }       // not null
    public string         Name        { get; set; } = ""; // not null
    public DateTimeOffset CreatedAt   { get; set; }       // timestamptz, not null
    public DateTimeOffset UpdatedAt   { get; set; }       // timestamptz, not null
}
