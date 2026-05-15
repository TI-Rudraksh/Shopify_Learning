namespace ShopifyIntegration.Domain.Entities;

public sealed class Customer
{
    public int            Id               { get; set; }       // PK, auto-increment
    public string         ShopifyGid       { get; set; } = ""; // unique, not null
    public long           NumericId        { get; set; }       // not null
    public string         Email            { get; set; } = ""; // not null
    public string         FirstName        { get; set; } = ""; // not null
    public string         LastName         { get; set; } = ""; // not null
    public string?        Phone            { get; set; }       // nullable
    public bool           AcceptsMarketing { get; set; }       // not null
    public DateTimeOffset CreatedAt        { get; set; }       // timestamptz, not null
    public DateTimeOffset UpdatedAt        { get; set; }       // timestamptz, not null
}
