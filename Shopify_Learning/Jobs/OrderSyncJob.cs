using Microsoft.EntityFrameworkCore;
using ShopifySharp;
using ShopifySharp.Services.Graph;
using ShopifyIntegration.Domain.Entities;
using ShopifyIntegration.Infrastructure.Data;
using ShopifyIntegration.Infrastructure.Data.Helpers;
using ShopifyIntegration.GraphQL.Queries;
using ShopifyIntegration.GraphQL.Responses.Orders;
using OrderEntity = ShopifyIntegration.Domain.Entities.Order;

namespace ShopifyIntegration.Jobs;

/// <summary>
/// Pulls orders updated in the last 30 minutes from Shopify and upserts them locally.
/// This is the safety net for missed webhooks — Shopify webhooks are not guaranteed
/// delivery, so this job ensures the local DB stays in sync.
/// Runs every 15 minutes.
/// </summary>
public sealed class OrderSyncJob
{
    private readonly ShopifyDbContext _db;
    private readonly GraphService     _graphService;
    private readonly ILogger<OrderSyncJob> _logger;

    public OrderSyncJob(
        ShopifyDbContext db,
        IConfiguration configuration,
        ILogger<OrderSyncJob> logger)
    {
        _db           = db;
        _graphService = new GraphService(
            configuration["Shopify:StoreUrl"],
            configuration["Shopify:AccessToken"]);
        _logger = logger;
    }

    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        // Look back 30 minutes to catch anything the last run may have missed
        var updatedAtMin = DateTimeOffset.UtcNow.AddMinutes(-30);
        var queryFilter  = $"updated_at:>={updatedAtMin:yyyy-MM-ddTHH:mm:ssZ}";

        _logger.LogInformation(
            "OrderSyncJob: syncing orders updated since {Since}.", updatedAtMin);

        string? cursor      = null;
        var     totalSynced = 0;

        do
        {
            // Shopify's `query:` argument does NOT support GraphQL variables —
            // it only accepts an inline string literal. We build the query text
            // dynamically so the filter is embedded directly in the GQL string.
            var afterArg   = cursor is not null ? $", after: \"{cursor}\"" : "";
            var inlineQuery = "{ orders(first: 25" + afterArg + ", sortKey: UPDATED_AT, reverse: true, query: \"" + queryFilter + "\") { " +
                              "pageInfo { hasNextPage endCursor } " +
                              "edges { node { id name displayFinancialStatus displayFulfillmentStatus " +
                              "totalPriceSet { shopMoney { amount currencyCode } } " +
                              "note customAttributes { key value } createdAt updatedAt cancelledAt " +
                              "customer { id } " +
                              "lineItems(first: 50) { edges { node { id title variantTitle quantity " +
                              "originalUnitPriceSet { shopMoney { amount } } sku product { id } variant { id } } } } } } } }";

            var request  = new GraphRequest { Query = inlineQuery };
            var response = await _graphService.PostAsync<OrderSyncResponse>(request);
            var orders   = response.Data?.Orders;

            if (orders?.Edges is null) break;

            foreach (var edge in orders.Edges)
            {
                var node = edge.Node;
                if (node?.Id is null) continue;

                await UpsertOrderAsync(node, ct);
                totalSynced++;
            }

            cursor = orders.PageInfo?.HasNextPage == true ? orders.PageInfo.EndCursor : null;

        } while (cursor is not null);

        _logger.LogInformation("OrderSyncJob: synced {Count} order(s).", totalSynced);
    }

    private async Task UpsertOrderAsync(OrderSyncNode node, CancellationToken ct)
    {
        var numericId = ShopifyGidHelper.ParseNumericId(node.Id!);

        var existing = await _db.Orders
            .Include(o => o.LineItems)
            .Include(o => o.NoteAttributes)
            .FirstOrDefaultAsync(o => o.ShopifyGid == node.Id, ct);

        var lineItems = (node.LineItems?.Edges ?? [])
            .Where(e => e.Node?.Id is not null)
            .Select(e => new OrderLineItem
            {
                ShopifyGid   = e.Node!.Id!,
                NumericId    = ShopifyGidHelper.ParseNumericId(e.Node.Id!),
                Title        = e.Node.Title        ?? "",
                VariantTitle = e.Node.VariantTitle  ?? "",
                Quantity     = e.Node.Quantity,
                Price        = decimal.TryParse(
                    e.Node.OriginalUnitPriceSet?.ShopMoney?.Amount, out var p) ? p : 0m,
                Sku          = e.Node.Sku           ?? "",
                ProductGid   = e.Node.Product?.Id   ?? "",
                VariantGid   = e.Node.Variant?.Id   ?? "",
            }).ToList();

        var noteAttributes = (node.CustomAttributes ?? [])
            .Select(a => new OrderNoteAttribute
            {
                Name  = a.Key   ?? "",
                Value = a.Value ?? "",
            }).ToList();

        if (existing is null)
        {
            var order = new OrderEntity
            {
                ShopifyGid        = node.Id!,
                NumericId         = numericId,
                Name              = node.Name              ?? "",
                FinancialStatus   = node.DisplayFinancialStatus   ?? "",
                FulfillmentStatus = node.DisplayFulfillmentStatus ?? "unfulfilled",
                TotalPrice        = decimal.TryParse(
                    node.TotalPriceSet?.ShopMoney?.Amount, out var tp) ? tp : 0m,
                Currency          = node.TotalPriceSet?.ShopMoney?.CurrencyCode ?? "",
                Note              = node.Note,
                CancelledAt       = node.CancelledAt?.ToUniversalTime(),
                CreatedAt         = node.CreatedAt.ToUniversalTime(),
                UpdatedAt         = node.UpdatedAt.ToUniversalTime(),
                LineItems         = lineItems,
                NoteAttributes    = noteAttributes,
            };
            _db.Orders.Add(order);
        }
        else
        {
            existing.Name              = node.Name              ?? "";
            existing.FinancialStatus   = node.DisplayFinancialStatus   ?? "";
            existing.FulfillmentStatus = node.DisplayFulfillmentStatus ?? "unfulfilled";
            existing.TotalPrice        = decimal.TryParse(
                node.TotalPriceSet?.ShopMoney?.Amount, out var tp) ? tp : 0m;
            existing.Currency          = node.TotalPriceSet?.ShopMoney?.CurrencyCode ?? "";
            existing.Note              = node.Note;
            existing.CancelledAt       = node.CancelledAt?.ToUniversalTime();
            existing.UpdatedAt         = node.UpdatedAt.ToUniversalTime();

            _db.OrderLineItems.RemoveRange(existing.LineItems);
            existing.LineItems = lineItems;

            _db.OrderNoteAttributes.RemoveRange(existing.NoteAttributes);
            existing.NoteAttributes = noteAttributes;
        }

        await _db.SaveChangesAsync(ct);
    }
}
