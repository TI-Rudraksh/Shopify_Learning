namespace ShopifyIntegration.Infrastructure.Shopify;

public sealed class ShopifyInventoryException : Exception
{
    public IReadOnlyList<string> ShopifyErrors { get; }

    public ShopifyInventoryException(IEnumerable<string> errors)
        : base($"Shopify inventory error(s): {string.Join("; ", errors)}")
    {
        ShopifyErrors = errors.ToList().AsReadOnly();
    }
}
