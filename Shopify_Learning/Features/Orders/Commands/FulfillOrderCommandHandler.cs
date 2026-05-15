using MediatR;
using ShopifyIntegration.Domain.Entities;
using ShopifyIntegration.Domain.Repositories;
using ShopifyIntegration.Infrastructure.Data.Helpers;
using ShopifyIntegration.Infrastructure.Shopify;

namespace ShopifyIntegration.Features.Orders.Commands;

public sealed class FulfillOrderCommandHandler
    : IRequestHandler<FulfillOrderCommand, FulfillOrderResult>
{
    private readonly IOrderRepository           _orders;
    private readonly IFulfillmentRepository     _fulfillments;
    private readonly IShopifyFulfillmentService _shopifyFulfillment;
    private readonly ILogger<FulfillOrderCommandHandler> _logger;

    public FulfillOrderCommandHandler(
        IOrderRepository           orders,
        IFulfillmentRepository     fulfillments,
        IShopifyFulfillmentService shopifyFulfillment,
        ILogger<FulfillOrderCommandHandler> logger)
    {
        _orders             = orders;
        _fulfillments       = fulfillments;
        _shopifyFulfillment = shopifyFulfillment;
        _logger             = logger;
    }

    public async Task<FulfillOrderResult> Handle(
        FulfillOrderCommand command, CancellationToken cancellationToken)
    {
        // Step 1: Resolve the order GID — accept local Id, NumericId, or ShopifyGid
        var order = await _orders.GetByAnyIdAsync(command.OrderId, cancellationToken);

        string orderGid;
        if (order is not null)
        {
            orderGid = order.ShopifyGid;
        }
        else if (long.TryParse(command.OrderId, out var numericId))
        {
            // Order not in local DB yet — build the GID and let Shopify validate it
            orderGid = ShopifyGidHelper.BuildOrderGid(numericId);
        }
        else
        {
            // Treat the raw value as a GID directly
            orderGid = command.OrderId;
        }

        // Step 2: Call Shopify fulfillment service
        var payload = await _shopifyFulfillment.FulfillOrderAsync(
            orderGid,
            command.TrackingNumber,
            command.TrackingCompany,
            command.NotifyCustomer,
            cancellationToken);

        var shopifyFulfillment = payload.Fulfillment!;

        // TrackingInfo is returned as a list by Shopify — take the first entry
        var tracking = shopifyFulfillment.TrackingInfo?.FirstOrDefault();

        // Step 3: Persist the fulfillment locally (best-effort)
        if (order is not null)
        {
            var fulfillmentNumericId = ShopifyGidHelper.ParseNumericId(shopifyFulfillment.Id!);
            var now = DateTimeOffset.UtcNow;

            var fulfillment = new Fulfillment
            {
                ShopifyGid      = shopifyFulfillment.Id!,
                NumericId       = fulfillmentNumericId,
                OrderId         = order.Id,
                Status          = shopifyFulfillment.Status ?? "success",
                TrackingNumber  = tracking?.Number,
                TrackingCompany = tracking?.Company,
                TrackingUrl     = tracking?.Url,
                CreatedAt       = now,
                UpdatedAt       = now,
            };

            await _fulfillments.UpsertAsync(fulfillment, cancellationToken);

            // Update order fulfillment status locally
            order.FulfillmentStatus = "fulfilled";
            order.UpdatedAt         = now;
            await _orders.UpsertAsync(order, cancellationToken);
        }
        else
        {
            _logger.LogWarning(
                "Order {OrderId} not found in local DB — fulfillment recorded in Shopify but not persisted locally.",
                command.OrderId);
        }

        _logger.LogInformation(
            "Order {OrderGid} fulfilled. FulfillmentGid={FulfillmentGid}",
            orderGid, shopifyFulfillment.Id);

        return new FulfillOrderResult(
            OrderGid:        orderGid,
            FulfillmentGid:  shopifyFulfillment.Id!,
            Status:          shopifyFulfillment.Status ?? "success",
            TrackingNumber:  tracking?.Number,
            TrackingCompany: tracking?.Company,
            TrackingUrl:     tracking?.Url);
    }
}
