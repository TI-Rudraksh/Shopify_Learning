using ShopifySharp;
using ShopifySharp.Services.Graph;
using ShopifyIntegration.GraphQL.Mutations;
using ShopifyIntegration.GraphQL.Queries;
using ShopifyIntegration.GraphQL.Responses.Orders;

namespace ShopifyIntegration.Infrastructure.Shopify;

public sealed class ShopifyOrderService : IShopifyOrderService
{
    private readonly GraphService _graphService;
    private readonly ILogger<ShopifyOrderService> _logger;

    public ShopifyOrderService(
        IConfiguration configuration,
        ILogger<ShopifyOrderService> logger)
    {
        var shopUrl     = configuration["Shopify:StoreUrl"];
        var accessToken = configuration["Shopify:AccessToken"];

        _graphService = new GraphService(shopUrl, accessToken);
        _logger       = logger;
    }

    private async Task<T?> ExecuteAsync<T>(string query, Dictionary<string, object>? variables = null)
    {
        var request  = new GraphRequest { Query = query, Variables = variables };
        var response = await _graphService.PostAsync<T>(request);
        return response.Data;
    }

    public async Task<ShopifyOrderNode?> GetOrderNoteAsync(
        string            orderGid,
        CancellationToken ct = default)
    {
        var variables = new Dictionary<string, object> { ["orderId"] = orderGid };
        var response  = await ExecuteAsync<GetOrderNoteResponse>(OrderQueries.GetOrderNote, variables);
        return response?.Order;
    }

    public async Task<OrderUpdatePayload> UpdateOrderNoteAsync(
        string                                    orderGid,
        string?                                   note,
        IEnumerable<(string Name, string Value)>? noteAttributes,
        CancellationToken                         ct = default)
    {
        var input = new Dictionary<string, object> { ["id"] = orderGid };

        if (note is not null)
            input["note"] = note;

        if (noteAttributes is not null)
        {
            input["customAttributes"] = noteAttributes
                .Select(a => (object)new Dictionary<string, object>
                {
                    ["key"]   = a.Name,
                    ["value"] = a.Value
                })
                .ToList();
        }

        var variables = new Dictionary<string, object> { ["input"] = input };

        var response = await ExecuteAsync<OrderUpdateResponse>(OrderMutations.OrderUpdate, variables);

        var payload    = response?.OrderUpdate;
        var userErrors = payload?.UserErrors;

        if (userErrors is { Count: > 0 })
        {
            var messages = userErrors
                .Select(e => e.Message ?? "(no message)")
                .ToList();

            _logger.LogWarning(
                "Shopify returned userErrors when updating order {OrderGid}: {Errors}",
                orderGid, string.Join("; ", messages));

            throw new ShopifyOrderException(messages);
        }

        _logger.LogInformation(
            "Successfully updated note/attributes for order {OrderGid}.", orderGid);

        return payload!;
    }
}
