namespace ShopifyIntegration.Domain.Entities;

public sealed class WebhookEvent
{
    public int            Id               { get; set; }       // PK, auto-increment
    public string         Topic            { get; set; } = ""; // not null
    public long?          ShopifyNumericId { get; set; }       // nullable bigint
    public string         RawPayload       { get; set; } = ""; // not null
    public DateTimeOffset ProcessedAt      { get; set; }       // timestamptz, not null
    public string         Status           { get; set; } = ""; // not null ("processed"|"failed"|"skipped")
    public string?        ErrorMessage     { get; set; }       // nullable
}
