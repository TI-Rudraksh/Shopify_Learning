using ShopifySharp;
using ShopifySharp.Services.Graph;
using ShopifyIntegration.GraphQL.Mutations;
using ShopifyIntegration.GraphQL.Queries;
using ShopifyIntegration.GraphQL.Responses.Inventory;

namespace ShopifyIntegration.Infrastructure.Shopify;

public class ShopifyInventoryService : IShopifyInventoryService
{
    private readonly GraphService _graphService;
    private readonly ILogger<ShopifyInventoryService> _logger;

    public ShopifyInventoryService(
        IConfiguration configuration,
        ILogger<ShopifyInventoryService> logger)
    {
        var shopUrl = configuration["Shopify:StoreUrl"];
        var accessToken = configuration["Shopify:AccessToken"];

        _graphService = new GraphService(shopUrl, accessToken);
        _logger = logger;
    }

    private async Task<T?> ExecuteAsync<T>(
        string query,
        Dictionary<string, object>? variables = null)
    {
        var request = new GraphRequest
        {
            Query = query,
            Variables = variables
        };

        var response = await _graphService.PostAsync<T>(request);

        return response.Data;
    }

    public async Task<string> GetInventoryItemGidAsync(
        string productGid,
        CancellationToken ct = default)
    {
        var variables = new Dictionary<string, object>
        {
            ["id"] = productGid
        };

        var response = await ExecuteAsync<GetInventoryItemGidResponse>(
            InventoryQueries.GetInventoryItemGid,
            variables);

        var inventoryItemGid = response
            ?.Product
            ?.Variants
            ?.Edges
            ?[0]
            ?.Node
            ?.InventoryItem
            ?.Id;

        if (inventoryItemGid is null)
        {
            _logger.LogError(
                "Failed to retrieve InventoryItemGid for product {ProductGid}.",
                productGid);
            throw new InvalidOperationException(
                $"Could not resolve InventoryItemGid for product '{productGid}'.");
        }

        return inventoryItemGid;
    }

    public async Task ActivateInventoryItemAsync(
        string inventoryItemGid,
        string locationGid,
        CancellationToken ct = default)
    {
        var variables = new Dictionary<string, object>
        {
            ["inventoryItemId"] = inventoryItemGid,
            ["inventoryItemUpdates"] = new[]
            {
                new Dictionary<string, object>
                {
                    ["locationId"] = locationGid,
                    ["activate"]   = true
                }
            }
        };

        var response = await ExecuteAsync<ActivateInventoryItemResponse>(
            InventoryMutations.ActivateInventoryItem,
            variables);

        var userErrors = response?.InventoryBulkToggleActivation?.UserErrors;
        if (userErrors is { Count: > 0 })
        {
            var messages = userErrors
                .Select(e => e.Message ?? "(no message)")
                .ToList();

            _logger.LogWarning(
                "Shopify returned userErrors when activating inventory item {InventoryItemGid} at {LocationGid}: {Errors}",
                inventoryItemGid, locationGid, string.Join("; ", messages));

            throw new ShopifyInventoryException(messages);
        }

        _logger.LogInformation(
            "Activated inventory item {InventoryItemGid} at location {LocationGid}.",
            inventoryItemGid, locationGid);
    }

    public async Task<SetOnHandQuantityResponse?> SetOnHandQuantityAsync(
        string inventoryItemGid,
        string locationGid,
        int quantity,
        CancellationToken ct = default)
    {
        var variables = new Dictionary<string, object>
        {
            ["input"] = new Dictionary<string, object>
            {
                ["reason"] = "correction",
                ["setQuantities"] = new[]
                {
                    new Dictionary<string, object>
                    {
                        ["inventoryItemId"] = inventoryItemGid,
                        ["locationId"]      = locationGid,
                        ["quantity"]        = quantity
                    }
                }
            }
        };

        var response = await ExecuteAsync<SetOnHandQuantityResponse>(
            InventoryMutations.SetOnHandQuantities,
            variables);

        var userErrors = response?.InventorySetOnHandQuantities?.UserErrors;
        if (userErrors is { Count: > 0 })
        {
            var messages = userErrors
                .Select(e => e.Message ?? "(no message)")
                .ToList();

            _logger.LogWarning(
                "Shopify returned userErrors when setting inventory for item {InventoryItemGid}: {Errors}",
                inventoryItemGid,
                string.Join("; ", messages));

            throw new ShopifyInventoryException(messages);
        }

        return response;
    }
}
