using MediatR;

namespace ShopifyIntegration.Features.Orders.Commands;

public sealed record FulfillOrderCommand(
    string  OrderId,
    string? TrackingNumber  = null,
    string? TrackingCompany = null,
    bool    NotifyCustomer  = true)
    : IRequest<FulfillOrderResult>;

public sealed record FulfillOrderResult(
    string  OrderGid,
    string  FulfillmentGid,
    string  Status,
    string? TrackingNumber,
    string? TrackingCompany,
    string? TrackingUrl);
