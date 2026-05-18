using MediatR;
using ShopifyIntegration.Domain.Repositories;
using ShopifyIntegration.Infrastructure.Data.Helpers;
using ShopifyIntegration.Infrastructure.Shopify;

namespace ShopifyIntegration.Features.Orders.Queries;

public sealed class GetOrderNoteQueryHandler
    : IRequestHandler<GetOrderNoteQuery, GetOrderNoteResult>
{
    private readonly IOrderRepository                  _orders;
    private readonly IShopifyOrderService              _shopifyOrders;
    private readonly ILogger<GetOrderNoteQueryHandler> _logger;

    public GetOrderNoteQueryHandler(
        IOrderRepository                  orders,
        IShopifyOrderService              shopifyOrders,
        ILogger<GetOrderNoteQueryHandler> logger)
    {
        _orders        = orders;
        _shopifyOrders = shopifyOrders;
        _logger        = logger;
    }

    public async Task<GetOrderNoteResult> Handle(
        GetOrderNoteQuery query, CancellationToken cancellationToken)
    {
        // ── Step 1: Try local DB first ────────────────────────────────────────────
        var order = await _orders.GetByAnyIdAsync(query.OrderId, cancellationToken);

        if (order is not null)
        {
            var localAttributes = order.NoteAttributes
                .Select(na => new NoteAttributeDto(na.Name, na.Value))
                .ToList()
                .AsReadOnly();

            return new GetOrderNoteResult(
                OrderGid:       order.ShopifyGid,
                Note:           order.Note,
                NoteAttributes: localAttributes);
        }

        // ── Step 2: Order not in local DB — resolve GID and fetch from Shopify ───
        string orderGid;
        if (long.TryParse(query.OrderId, out var numericId))
        {
            orderGid = ShopifyGidHelper.BuildOrderGid(numericId);
        }
        else
        {
            // Treat the raw value as a GID directly
            orderGid = query.OrderId;
        }

        _logger.LogWarning(
            "Order {OrderId} not found in local DB — fetching note from Shopify.",
            query.OrderId);

        var shopifyOrder = await _shopifyOrders.GetOrderNoteAsync(orderGid, cancellationToken);

        if (shopifyOrder is null)
        {
            throw new KeyNotFoundException($"Order '{query.OrderId}' was not found in Shopify.");
        }

        var remoteAttributes = (shopifyOrder.CustomAttributes ?? [])
            .Select(a => new NoteAttributeDto(a.Key ?? "", a.Value ?? ""))
            .ToList()
            .AsReadOnly();

        return new GetOrderNoteResult(
            OrderGid:       shopifyOrder.Id ?? orderGid,
            Note:           shopifyOrder.Note,
            NoteAttributes: remoteAttributes);
    }
}
