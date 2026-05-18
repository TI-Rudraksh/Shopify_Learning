namespace ShopifyIntegration.Infrastructure.Shopify;

public sealed class ShopifyOrderException : Exception
{
    public IReadOnlyList<string> ShopifyErrors { get; }

    public ShopifyOrderException(IEnumerable<string> errors)
        : base($"Shopify order error(s): {string.Join("; ", errors)}")
    {
        ShopifyErrors = errors.ToList().AsReadOnly();
    }
}
