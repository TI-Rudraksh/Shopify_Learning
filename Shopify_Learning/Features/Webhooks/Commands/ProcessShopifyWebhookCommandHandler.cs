using System.Text;
using MediatR;
using Newtonsoft.Json;
using ShopifyIntegration.Domain.Entities;
using ShopifyIntegration.Domain.Repositories;
using ShopifyIntegration.Features.Webhooks.Models;

namespace ShopifyIntegration.Features.Webhooks.Commands;

public sealed class ProcessShopifyWebhookCommandHandler
    : IRequestHandler<ProcessShopifyWebhookCommand, Unit>
{
    private readonly IMediator _mediator;
    private readonly IWebhookEventRepository _webhookEvents;

    public ProcessShopifyWebhookCommandHandler(
        IMediator mediator,
        IWebhookEventRepository webhookEvents)
    {
        _mediator      = mediator;
        _webhookEvents = webhookEvents;
    }

    public async Task<Unit> Handle(
        ProcessShopifyWebhookCommand command, CancellationToken cancellationToken)
    {
        var json = Encoding.UTF8.GetString(command.RawBody);

        switch (command.Topic)
        {
            case "products/create":
            {
                var payload = JsonConvert.DeserializeObject<ProductCreatedWebhook>(json)!;
                await _mediator.Send(new HandleProductCreatedCommand(
                    payload.Id, payload.Title, payload.Vendor,
                    payload.Status, payload.UpdatedAt), cancellationToken);
                break;
            }
            case "products/update":
            {
                var payload = JsonConvert.DeserializeObject<ProductUpdatedWebhook>(json)!;
                await _mediator.Send(new HandleProductUpdatedCommand(
                    payload.Id, payload.Title, payload.Vendor,
                    payload.Status, payload.UpdatedAt), cancellationToken);
                break;
            }
            case "products/delete":
            {
                var payload = JsonConvert.DeserializeObject<ProductDeletedWebhook>(json)!;
                await _mediator.Send(
                    new HandleProductDeletedCommand(payload.Id), cancellationToken);
                break;
            }
            case "locations/create":
            {
                var payload = JsonConvert.DeserializeObject<LocationCreatedWebhook>(json)!;
                await _mediator.Send(new HandleLocationCreatedCommand(
                    payload.Id, payload.Name, payload.UpdatedAt), cancellationToken);
                break;
            }
            case "locations/update":
            {
                var payload = JsonConvert.DeserializeObject<LocationUpdatedWebhook>(json)!;
                await _mediator.Send(new HandleLocationUpdatedCommand(
                    payload.Id, payload.Name, payload.UpdatedAt), cancellationToken);
                break;
            }
            case "locations/delete":
            {
                var payload = JsonConvert.DeserializeObject<LocationDeletedWebhook>(json)!;
                await _mediator.Send(
                    new HandleLocationDeletedCommand(payload.Id), cancellationToken);
                break;
            }
            case "orders/create":
            {
                var payload = JsonConvert.DeserializeObject<OrderWebhook>(json)!;
                await _mediator.Send(new HandleOrderCreatedCommand(payload), cancellationToken);
                break;
            }
            case "orders/updated":
            {
                var payload = JsonConvert.DeserializeObject<OrderWebhook>(json)!;
                await _mediator.Send(new HandleOrderUpdatedCommand(payload), cancellationToken);
                break;
            }
            case "orders/fulfilled":
            {
                var payload = JsonConvert.DeserializeObject<OrderWebhook>(json)!;
                await _mediator.Send(new HandleOrderFulfilledCommand(payload), cancellationToken);
                break;
            }
            case "fulfillments/create":
            {
                var payload = JsonConvert.DeserializeObject<FulfillmentWebhook>(json)!;
                await _mediator.Send(new HandleFulfillmentCreatedCommand(payload), cancellationToken);
                break;
            }
            case "fulfillments/update":
            {
                var payload = JsonConvert.DeserializeObject<FulfillmentWebhook>(json)!;
                await _mediator.Send(new HandleFulfillmentUpdatedCommand(payload), cancellationToken);
                break;
            }
            case "inventory_levels/update":
            case "inventory_levels/connect":
            {
                var payload = JsonConvert.DeserializeObject<InventoryLevelWebhook>(json)!;
                await _mediator.Send(new HandleInventoryLevelUpdatedCommand(payload), cancellationToken);
                break;
            }
            default:
                await _webhookEvents.AddAsync(new WebhookEvent
                {
                    Topic       = command.Topic,
                    RawPayload  = json,
                    ProcessedAt = DateTimeOffset.UtcNow,
                    Status      = "skipped",
                }, cancellationToken);
                break;
        }

        return Unit.Value;
    }
}
