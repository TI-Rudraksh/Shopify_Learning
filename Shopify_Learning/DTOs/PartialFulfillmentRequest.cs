namespace ShopifyIntegration.DTOs;

public sealed class PartialFulfillmentRequest
{
    public List<string> LineItemIds     { get; set; } = [];
    public string?      TrackingNumber  { get; set; }
    public string?      TrackingCompany { get; set; }
    public bool         NotifyCustomer  { get; set; } = true;
}
