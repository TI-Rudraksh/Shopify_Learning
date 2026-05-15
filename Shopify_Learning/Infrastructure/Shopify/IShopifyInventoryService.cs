using ShopifyIntegration.GraphQL.Responses.Inventory;

namespace ShopifyIntegration.Infrastructure.Shopify;

public interface IShopifyInventoryService
{
    Task<string> GetInventoryItemGidAsync(string productGid, CancellationToken ct = default);
    Task ActivateInventoryItemAsync(string inventoryItemGid, string locationGid, CancellationToken ct = default);
    Task<SetOnHandQuantityResponse?> SetOnHandQuantityAsync(
        string inventoryItemGid,
        string locationGid,
        int quantity,
        CancellationToken ct = default);
}
