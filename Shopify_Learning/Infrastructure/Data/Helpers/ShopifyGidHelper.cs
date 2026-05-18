namespace ShopifyIntegration.Infrastructure.Data.Helpers;

public static class ShopifyGidHelper
{
    // Parses "gid://shopify/Product/123456789" → 123456789
    public static long ParseNumericId(string gid)
    {
        var lastSlash = gid.LastIndexOf('/');
        if (lastSlash < 0 || !long.TryParse(gid[(lastSlash + 1)..], out var id))
            throw new FormatException($"Cannot parse numeric id from Shopify GID: '{gid}'");
        return id;
    }

    // Builds "gid://shopify/Product/123456789" from a numeric id
    public static string BuildProductGid(long numericId) =>
        $"gid://shopify/Product/{numericId}";

    // Builds "gid://shopify/Location/123456789" from a numeric id
    public static string BuildLocationGid(long numericId) =>
        $"gid://shopify/Location/{numericId}";

    // Builds "gid://shopify/InventoryItem/123456789" from a numeric id
    public static string BuildInventoryItemGid(long numericId) =>
        $"gid://shopify/InventoryItem/{numericId}";

    // Builds "gid://shopify/Order/123456789" from a numeric id
    public static string BuildOrderGid(long numericId) =>
        $"gid://shopify/Order/{numericId}";

    // Builds "gid://shopify/Customer/123456789" from a numeric id
    public static string BuildCustomerGid(long numericId) =>
        $"gid://shopify/Customer/{numericId}";

    // Builds "gid://shopify/Fulfillment/123456789" from a numeric id
    public static string BuildFulfillmentGid(long numericId) =>
        $"gid://shopify/Fulfillment/{numericId}";

    // Builds "gid://shopify/LineItem/123456789" from a numeric id
    public static string BuildLineItemGid(long numericId) =>
        $"gid://shopify/LineItem/{numericId}";
}
