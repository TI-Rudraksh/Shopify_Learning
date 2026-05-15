namespace ShopifyIntegration.Infrastructure.Shopify;

public sealed class ShopifyFulfillmentException : Exception
{
    public IReadOnlyList<string> ShopifyErrors { get; }

    public ShopifyFulfillmentException(IEnumerable<string> errors)
        : base($"Shopify fulfillment error(s): {string.Join("; ", errors)}")
    {
        ShopifyErrors = errors.ToList().AsReadOnly();
    }
}
