using System.Text.Json;
using MediatR;
using ShopifyIntegration.Domain.Entities;
using ShopifyIntegration.Domain.Repositories;
using ShopifyIntegration.Infrastructure.Data.Helpers;
using ShopifyIntegration.Infrastructure.Shopify;

namespace ShopifyIntegration.Features.Orders.Commands;

public sealed class FulfillOrderLineItemsCommandHandler
    : IRequestHandler<FulfillOrderLineItemsCommand, FulfillOrderLineItemsResult>
{
    private readonly IOrderRepository                              _orders;
    private readonly IFulfillmentRepository                        _fulfillments;
    private readonly IShopifyFulfillmentService                    _shopifyFulfillment;
    private readonly ILogger<FulfillOrderLineItemsCommandHandler>  _logger;

    public FulfillOrderLineItemsCommandHandler(
        IOrderRepository                              orders,
        IFulfillmentRepository                        fulfillments,
        IShopifyFulfillmentService                    shopifyFulfillment,
        ILogger<FulfillOrderLineItemsCommandHandler>  logger)
    {
        _orders             = orders;
        _fulfillments       = fulfillments;
        _shopifyFulfillment = shopifyFulfillment;
        _logger             = logger;
    }

    public async Task<FulfillOrderLineItemsResult> Handle(
        FulfillOrderLineItemsCommand command, CancellationToken cancellationToken)
    {
        // ── Step 1: Resolve the order GID ────────────────────────────────────────
        // Three-step fallback identical to FulfillOrderCommandHandler:
        //   a) If the order exists locally, use its ShopifyGid.
        //   b) If the raw value is a numeric string, build the GID via ShopifyGidHelper.
        //   c) Otherwise treat the raw value as a GID directly.
        var order = await _orders.GetByAnyIdAsync(command.OrderId, cancellationToken);

        string orderGid;
        if (order is not null)
        {
            orderGid = order.ShopifyGid;
        }
        else if (long.TryParse(command.OrderId, out var numericOrderId))
        {
            orderGid = ShopifyGidHelper.BuildOrderGid(numericOrderId);
        }
        else
        {
            orderGid = command.OrderId;
        }

        // ── Step 2: Resolve each lineItemId to an OrderLineItem.ShopifyGid ───────
        // Resolution strategy per ID:
        //   a) Parseable as int → match OrderLineItem.Id (PK) first, then NumericId.
        //   b) Starts with "gid://shopify/" → match OrderLineItem.ShopifyGid directly.
        //   c) Otherwise → try numeric parse (long), then treat as raw GID.
        // Unresolved IDs are logged as warnings; processing continues.
        // If no IDs resolve at all, throw ShopifyFulfillmentException.

        var lineItems = order?.LineItems ?? new List<OrderLineItem>();
        var resolvedGids = new List<string>();

        foreach (var lineItemId in command.LineItemIds)
        {
            string? resolvedGid = null;

            if (int.TryParse(lineItemId, out var intId))
            {
                // Try local PK first, then NumericId
                var match = lineItems.FirstOrDefault(li => li.Id == intId)
                         ?? lineItems.FirstOrDefault(li => li.NumericId == intId);
                resolvedGid = match?.ShopifyGid;
            }
            else if (lineItemId.StartsWith("gid://shopify/", StringComparison.OrdinalIgnoreCase))
            {
                // Treat as a Shopify GID — match against ShopifyGid
                var match = lineItems.FirstOrDefault(li =>
                    string.Equals(li.ShopifyGid, lineItemId, StringComparison.OrdinalIgnoreCase));
                resolvedGid = match?.ShopifyGid ?? lineItemId; // pass through if not found locally
            }
            else if (long.TryParse(lineItemId, out var longId))
            {
                // Numeric string too large for int — match NumericId first,
                // then fall back to constructing the GID (same pattern as order ID resolution)
                var match = lineItems.FirstOrDefault(li => li.NumericId == longId);
                resolvedGid = match?.ShopifyGid ?? ShopifyGidHelper.BuildLineItemGid(longId);
            }
            else
            {
                // Treat as a raw GID
                resolvedGid = lineItemId;
            }

            if (resolvedGid is null)
            {
                // Requirement 3.3 / 10.3: log warning, continue
                _logger.LogWarning(
                    "Line item ID '{LineItemId}' could not be resolved to a known OrderLineItem — skipping.",
                    lineItemId);
            }
            else
            {
                resolvedGids.Add(resolvedGid);
            }
        }

        // Requirement 3.4: if nothing resolved, throw
        if (resolvedGids.Count == 0)
        {
            throw new ShopifyFulfillmentException(
                ["No line items could be resolved to known OrderLineItems."]);
        }

        // ── Step 3: Call Shopify fulfillment service ──────────────────────────────
        // Requirement 4.1: pass orderGid, resolved line item GIDs, and tracking/notification params
        var payload = await _shopifyFulfillment.FulfillLineItemsAsync(
            orderGid,
            resolvedGids,
            command.TrackingNumber,
            command.TrackingCompany,
            command.NotifyCustomer,
            cancellationToken);

        var shopifyFulfillment = payload.Fulfillment!;

        // TrackingInfo is returned as a list by Shopify — take the first entry (Requirement 7.3)
        var tracking = shopifyFulfillment.TrackingInfo?.FirstOrDefault();

        // ── Step 4: Persist fulfillment and update order status ───────────────────
        if (order is not null)
        {
            // Requirement 5.1, 5.2: build and persist the Fulfillment entity
            var fulfillmentNumericId = ShopifyGidHelper.ParseNumericId(shopifyFulfillment.Id!);
            var now = DateTimeOffset.UtcNow;

            var fulfillment = new Fulfillment
            {
                ShopifyGid            = shopifyFulfillment.Id!,
                NumericId             = fulfillmentNumericId,
                OrderId               = order.Id,
                Status                = shopifyFulfillment.Status ?? "success",
                TrackingNumber        = tracking?.Number,
                TrackingCompany       = tracking?.Company,
                TrackingUrl           = tracking?.Url,
                FulfilledLineItemGids = JsonSerializer.Serialize(resolvedGids),
                CreatedAt             = now,
                UpdatedAt             = now,
            };

            await _fulfillments.UpsertAsync(fulfillment, cancellationToken);

            // Requirement 6.1: reload all fulfillments for the order after upserting
            var allFulfillments = await _fulfillments.GetAllForOrderAsync(order.Id, cancellationToken);

            // Requirement 6.2: collect all distinct fulfilled OrderLineItem GIDs across all records
            var fulfilledGids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var f in allFulfillments)
            {
                if (string.IsNullOrWhiteSpace(f.FulfilledLineItemGids))
                    continue;

                var gids = JsonSerializer.Deserialize<List<string>>(f.FulfilledLineItemGids);
                if (gids is not null)
                {
                    foreach (var gid in gids)
                        fulfilledGids.Add(gid);
                }
            }

            // Requirements 5.3, 5.4, 6.3, 6.4: compare distinct fulfilled count vs total line items
            order.FulfillmentStatus = fulfilledGids.Count >= order.LineItems.Count
                ? "fulfilled"
                : "partial";
            order.UpdatedAt = now;

            await _orders.UpsertAsync(order, cancellationToken);
        }
        else
        {
            // Requirement 5.5, 10.2: order not found locally — log warning, skip persistence
            _logger.LogWarning(
                "Order {OrderId} not found in local DB — fulfillment recorded in Shopify but not persisted locally.",
                command.OrderId);
        }

        // Requirement 10.1: log informational message on success
        _logger.LogInformation(
            "Order {OrderGid} partially fulfilled. FulfillmentGid={FulfillmentGid}",
            orderGid, shopifyFulfillment.Id);

        // Requirement 7.1: return the result record
        return new FulfillOrderLineItemsResult(
            OrderGid:        orderGid,
            FulfillmentGid:  shopifyFulfillment.Id!,
            Status:          shopifyFulfillment.Status ?? "success",
            TrackingNumber:  tracking?.Number,
            TrackingCompany: tracking?.Company,
            TrackingUrl:     tracking?.Url);
    }
}
