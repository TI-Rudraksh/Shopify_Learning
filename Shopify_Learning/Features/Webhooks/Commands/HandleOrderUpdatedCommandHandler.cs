using MediatR;
using ShopifyIntegration.Domain.Entities;
using ShopifyIntegration.Domain.Repositories;
using ShopifyIntegration.Infrastructure.Data.Helpers;

namespace ShopifyIntegration.Features.Webhooks.Commands;

public sealed class HandleOrderUpdatedCommandHandler
    : IRequestHandler<HandleOrderUpdatedCommand, Unit>
{
    private readonly IOrderRepository        _orders;
    private readonly ICustomerRepository     _customers;
    private readonly IWebhookEventRepository _webhookEvents;

    public HandleOrderUpdatedCommandHandler(
        IOrderRepository        orders,
        ICustomerRepository     customers,
        IWebhookEventRepository webhookEvents)
    {
        _orders        = orders;
        _customers     = customers;
        _webhookEvents = webhookEvents;
    }

    public async Task<Unit> Handle(
        HandleOrderUpdatedCommand command, CancellationToken cancellationToken)
    {
        var payload = command.Payload;

        try
        {
            // Upsert customer if present
            int? customerId = null;
            if (payload.Customer is not null)
            {
                var customer = new Customer
                {
                    ShopifyGid       = ShopifyGidHelper.BuildCustomerGid(payload.Customer.Id),
                    NumericId        = payload.Customer.Id,
                    Email            = payload.Customer.Email,
                    FirstName        = payload.Customer.FirstName,
                    LastName         = payload.Customer.LastName,
                    Phone            = payload.Customer.Phone,
                    AcceptsMarketing = payload.Customer.AcceptsMarketing,
                    CreatedAt        = DateTimeOffset.UtcNow,
                    UpdatedAt        = payload.Customer.UpdatedAt.ToUniversalTime(),
                };
                var saved = await _customers.UpsertAsync(customer, cancellationToken);
                customerId = saved.Id;
            }

            var order = new Order
            {
                ShopifyGid        = ShopifyGidHelper.BuildOrderGid(payload.Id),
                NumericId         = payload.Id,
                Name              = payload.Name,
                FinancialStatus   = payload.FinancialStatus,
                FulfillmentStatus = payload.FulfillmentStatus ?? "unfulfilled",
                TotalPrice        = decimal.Parse(payload.TotalPrice),
                Currency          = payload.Currency,
                CustomerId        = customerId,
                CancelledAt       = payload.CancelledAt?.ToUniversalTime(),
                CreatedAt         = payload.CreatedAt.ToUniversalTime(),
                UpdatedAt         = payload.UpdatedAt.ToUniversalTime(),
                LineItems         = payload.LineItems.Select(li => new OrderLineItem
                {
                    ShopifyGid   = $"gid://shopify/LineItem/{li.Id}",
                    NumericId    = li.Id,
                    Title        = li.Title,
                    VariantTitle = li.VariantTitle ?? string.Empty,
                    Quantity     = li.Quantity,
                    Price        = decimal.Parse(li.Price),
                    Sku          = li.Sku ?? string.Empty,
                    ProductGid   = li.ProductId.HasValue ? ShopifyGidHelper.BuildProductGid(li.ProductId.Value) : string.Empty,
                    VariantGid   = li.VariantId.HasValue ? $"gid://shopify/ProductVariant/{li.VariantId.Value}" : string.Empty,
                }).ToList<OrderLineItem>(),
            };

            await _orders.UpsertAsync(order, cancellationToken);

            await _webhookEvents.AddAsync(new WebhookEvent
            {
                Topic            = "orders/updated",
                ShopifyNumericId = payload.Id,
                RawPayload       = "",
                ProcessedAt      = DateTimeOffset.UtcNow,
                Status           = "processed",
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            await _webhookEvents.AddAsync(new WebhookEvent
            {
                Topic            = "orders/updated",
                ShopifyNumericId = payload.Id,
                RawPayload       = "",
                ProcessedAt      = DateTimeOffset.UtcNow,
                Status           = "failed",
                ErrorMessage     = ex.Message,
            }, cancellationToken);
            throw;
        }

        return Unit.Value;
    }
}
