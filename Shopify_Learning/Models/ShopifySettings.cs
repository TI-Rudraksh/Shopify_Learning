namespace ShopifyIntegration.Models;

public class ShopifySettings
{
    public string StoreUrl { get; set; } = string.Empty;
    public string AccessToken { get; set; } = string.Empty;
    public string ApiVersion { get; set; } = string.Empty;
    public string WebhookSecret { get; set; } = string.Empty;
}