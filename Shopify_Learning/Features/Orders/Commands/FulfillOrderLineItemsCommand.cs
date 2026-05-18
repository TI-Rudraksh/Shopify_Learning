using MediatR;

namespace ShopifyIntegration.Features.Orders.Commands;

public sealed record FulfillOrderLineItemsCommand(
    string       OrderId,
    List<string> LineItemIds,
    string?      TrackingNumber  = null,
    string?      TrackingCompany = null,
    bool         NotifyCustomer  = true)
    : IRequest<FulfillOrderLineItemsResult>;

public sealed record FulfillOrderLineItemsResult(
    string  OrderGid,
    string  FulfillmentGid,
    string  Status,
    string? TrackingNumber,
    string? TrackingCompany,
    string? TrackingUrl);
