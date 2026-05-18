using ShopifySharp;
using ShopifySharp.Services.Graph;
using ShopifyIntegration.GraphQL.Mutations;
using ShopifyIntegration.GraphQL.Queries;
using ShopifyIntegration.GraphQL.Responses.Fulfillment;

namespace ShopifyIntegration.Infrastructure.Shopify;

public sealed class ShopifyFulfillmentService : IShopifyFulfillmentService
{
    private readonly GraphService _graphService;
    private readonly ILogger<ShopifyFulfillmentService> _logger;

    public ShopifyFulfillmentService(
        IConfiguration configuration,
        ILogger<ShopifyFulfillmentService> logger)
    {
        var shopUrl     = configuration["Shopify:StoreUrl"];
        var accessToken = configuration["Shopify:AccessToken"];

        _graphService = new GraphService(shopUrl, accessToken);
        _logger       = logger;
    }

    private async Task<T?> ExecuteAsync<T>(string query, Dictionary<string, object>? variables = null)
    {
        var request = new GraphRequest { Query = query, Variables = variables };
        var response = await _graphService.PostAsync<T>(request);
        return response.Data;
    }

    public async Task<FulfillmentCreatePayload> FulfillOrderAsync(
        string  orderGid,
        string? trackingNumber  = null,
        string? trackingCompany = null,
        bool    notifyCustomer  = true,
        CancellationToken ct    = default)
    {
        // Step 1: Fetch open fulfillment orders for this order
        var variables = new Dictionary<string, object> { ["orderId"] = orderGid };

        var ordersResponse = await ExecuteAsync<GetFulfillmentOrdersResponse>(
            FulfillmentQueries.GetFulfillmentOrders, variables);

        var fulfillmentOrderIds = ordersResponse
            ?.Order
            ?.FulfillmentOrders
            ?.Edges
            ?.Where(e => e.Node?.Status is "OPEN" or "IN_PROGRESS")
            .Select(e => e.Node!.Id!)
            .ToList() ?? [];

        if (fulfillmentOrderIds.Count == 0)
        {
            _logger.LogWarning(
                "No open fulfillment orders found for order {OrderGid}.", orderGid);
            throw new ShopifyFulfillmentException(
                ["No open fulfillment orders found for the given order."]);
        }

        // Step 2: Build the fulfillment input
        var fulfillmentInput = new Dictionary<string, object>
        {
            ["notifyCustomer"]    = notifyCustomer,
            ["lineItemsByFulfillmentOrder"] = fulfillmentOrderIds
                .Select(id => new Dictionary<string, object> { ["fulfillmentOrderId"] = id })
                .ToList<object>()
        };

        if (trackingNumber is not null || trackingCompany is not null)
        {
            var trackingInfo = new Dictionary<string, object>();
            if (trackingNumber  is not null) trackingInfo["number"]  = trackingNumber;
            if (trackingCompany is not null) trackingInfo["company"] = trackingCompany;
            fulfillmentInput["trackingInfo"] = trackingInfo;
        }

        // Step 3: Call fulfillmentCreate mutation
        var mutationVariables = new Dictionary<string, object>
        {
            ["fulfillment"] = fulfillmentInput
        };

        var response = await ExecuteAsync<FulfillmentCreateResponse>(
            FulfillmentMutations.FulfillmentCreate, mutationVariables);

        var payload = response?.FulfillmentCreate;

        var userErrors = payload?.UserErrors;
        if (userErrors is { Count: > 0 })
        {
            var messages = userErrors
                .Select(e => e.Message ?? "(no message)")
                .ToList();

            _logger.LogWarning(
                "Shopify returned userErrors when fulfilling order {OrderGid}: {Errors}",
                orderGid, string.Join("; ", messages));

            throw new ShopifyFulfillmentException(messages);
        }

        _logger.LogInformation(
            "Successfully fulfilled order {OrderGid}. Fulfillment GID: {FulfillmentGid}",
            orderGid, payload?.Fulfillment?.Id);

        return payload!;
    }

    public async Task<FulfillmentCreatePayload> FulfillLineItemsAsync(
        string            orderGid,
        List<string>      lineItemGids,
        string?           trackingNumber  = null,
        string?           trackingCompany = null,
        bool              notifyCustomer  = true,
        CancellationToken ct              = default)
    {
        // Step 1: Fetch fulfillment orders with line items for this order
        var variables = new Dictionary<string, object> { ["orderId"] = orderGid };

        var ordersResponse = await ExecuteAsync<GetFulfillmentOrdersWithLineItemsResponse>(
            FulfillmentQueries.GetFulfillmentOrdersWithLineItems, variables);

        // Step 2: Filter to OPEN or IN_PROGRESS fulfillment orders
        var openFulfillmentOrders = ordersResponse
            ?.Order
            ?.FulfillmentOrders
            ?.Edges
            ?.Where(e => e.Node?.Status is "OPEN" or "IN_PROGRESS")
            .Select(e => e.Node!)
            .ToList() ?? [];

        // Step 3 & 4: For each FulfillmentOrderLineItem, check if lineItem.id is in the
        // requested lineItemGids set, then group matched FulfillmentOrderLineItem GIDs
        // by parent FulfillmentOrder GID
        var lineItemGidSet = new HashSet<string>(lineItemGids, StringComparer.OrdinalIgnoreCase);
        var groupedByFulfillmentOrder = new Dictionary<string, List<(string Id, int Quantity)>>();

        foreach (var fulfillmentOrder in openFulfillmentOrders)
        {
            if (fulfillmentOrder.Id is null) continue;

            var lineItemEdges = fulfillmentOrder.LineItems?.Edges ?? [];
            foreach (var edge in lineItemEdges)
            {
                var node = edge.Node;
                if (node?.Id is null || node.LineItem?.Id is null) continue;

                // Check if this FulfillmentOrderLineItem's backing OrderLineItem GID
                // is in the requested set
                if (!lineItemGidSet.Contains(node.LineItem.Id)) continue;

                if (!groupedByFulfillmentOrder.TryGetValue(fulfillmentOrder.Id, out var list))
                {
                    list = [];
                    groupedByFulfillmentOrder[fulfillmentOrder.Id] = list;
                }
                // Use remainingQuantity (must be > 0); fall back to 1 as a safe default
                var qty = node.RemainingQuantity > 0 ? node.RemainingQuantity : 1;
                list.Add((node.Id, qty));
            }
        }

        // Step 5: If no matches found, throw
        if (groupedByFulfillmentOrder.Count == 0)
        {
            _logger.LogWarning(
                "No matching FulfillmentOrderLineItems found for order {OrderGid} with requested line item GIDs.",
                orderGid);
            throw new ShopifyFulfillmentException(
                ["None of the requested line items were found in any open fulfillment order."]);
        }

        // Step 6: Build lineItemsByFulfillmentOrder input and call fulfillmentCreate mutation
        var fulfillmentInput = new Dictionary<string, object>
        {
            ["notifyCustomer"] = notifyCustomer,
            ["lineItemsByFulfillmentOrder"] = groupedByFulfillmentOrder
                .Select(kvp => (object)new Dictionary<string, object>
                {
                    ["fulfillmentOrderId"]       = kvp.Key,
                    ["fulfillmentOrderLineItems"] = kvp.Value
                        .Select(item => (object)new Dictionary<string, object>
                        {
                            ["id"]       = item.Id,
                            ["quantity"] = item.Quantity
                        })
                        .ToList()
                })
                .ToList()
        };

        if (trackingNumber is not null || trackingCompany is not null)
        {
            var trackingInfo = new Dictionary<string, object>();
            if (trackingNumber  is not null) trackingInfo["number"]  = trackingNumber;
            if (trackingCompany is not null) trackingInfo["company"] = trackingCompany;
            fulfillmentInput["trackingInfo"] = trackingInfo;
        }

        var mutationVariables = new Dictionary<string, object>
        {
            ["fulfillment"] = fulfillmentInput
        };

        var response = await ExecuteAsync<FulfillmentCreateResponse>(
            FulfillmentMutations.FulfillmentCreate, mutationVariables);

        var payload = response?.FulfillmentCreate;

        var userErrors = payload?.UserErrors;
        if (userErrors is { Count: > 0 })
        {
            var messages = userErrors
                .Select(e => e.Message ?? "(no message)")
                .ToList();

            _logger.LogWarning(
                "Shopify returned userErrors when fulfilling line items for order {OrderGid}: {Errors}",
                orderGid, string.Join("; ", messages));

            throw new ShopifyFulfillmentException(messages);
        }

        _logger.LogInformation(
            "Successfully fulfilled line items for order {OrderGid}. Fulfillment GID: {FulfillmentGid}",
            orderGid, payload?.Fulfillment?.Id);

        return payload!;
    }
}
