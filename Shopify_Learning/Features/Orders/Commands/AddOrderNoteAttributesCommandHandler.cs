using MediatR;
using ShopifyIntegration.Domain.Entities;
using ShopifyIntegration.Domain.Repositories;
using ShopifyIntegration.Infrastructure.Data.Helpers;
using ShopifyIntegration.Infrastructure.Shopify;

namespace ShopifyIntegration.Features.Orders.Commands;

public sealed class AddOrderNoteAttributesCommandHandler
    : IRequestHandler<AddOrderNoteAttributesCommand, AddOrderNoteAttributesResult>
{
    private readonly IOrderRepository                              _orders;
    private readonly IShopifyOrderService                          _shopifyOrders;
    private readonly ILogger<AddOrderNoteAttributesCommandHandler> _logger;

    public AddOrderNoteAttributesCommandHandler(
        IOrderRepository                              orders,
        IShopifyOrderService                          shopifyOrders,
        ILogger<AddOrderNoteAttributesCommandHandler> logger)
    {
        _orders        = orders;
        _shopifyOrders = shopifyOrders;
        _logger        = logger;
    }

    public async Task<AddOrderNoteAttributesResult> Handle(
        AddOrderNoteAttributesCommand command, CancellationToken cancellationToken)
    {
        // ── Step 1: Resolve the order GID ────────────────────────────────────────
        var order = await _orders.GetByAnyIdAsync(command.OrderId, cancellationToken);

        string orderGid;
        if (order is not null)
        {
            orderGid = order.ShopifyGid;
        }
        else if (long.TryParse(command.OrderId, out var numericId))
        {
            orderGid = ShopifyGidHelper.BuildOrderGid(numericId);
        }
        else
        {
            orderGid = command.OrderId;
        }

        // ── Step 2: Build the merged attribute list to send to Shopify ───────────
        // Additive: existing attributes are preserved; incoming ones with the same
        // name overwrite, new names are appended.
        IEnumerable<(string Name, string Value)>? mergedAttrs = null;

        if (command.Attributes.Count > 0)
        {
            var existingAttrs = order?.NoteAttributes
                .Select(na => (na.Name, na.Value))
                ?? Enumerable.Empty<(string, string)>();

            var newAttrsDict = command.Attributes
                .ToDictionary(a => a.Name, a => a.Value, StringComparer.OrdinalIgnoreCase);

            mergedAttrs = existingAttrs
                .Where(a => !newAttrsDict.ContainsKey(a.Name))
                .Concat(command.Attributes.Select(a => (a.Name, a.Value)))
                .ToList();
        }

        // ── Step 3: Sync to Shopify ───────────────────────────────────────────────
        await _shopifyOrders.UpdateOrderNoteAsync(
            orderGid,
            note:           command.Note,
            noteAttributes: mergedAttrs,
            ct:             cancellationToken);

        // ── Step 4: Persist locally ───────────────────────────────────────────────
        if (order is not null)
        {
            // Update the note text on the order if provided
            if (command.Note is not null)
            {
                order.Note      = command.Note;
                order.UpdatedAt = DateTimeOffset.UtcNow;
                await _orders.UpsertAsync(order, cancellationToken);
            }

            // Append new note attributes
            if (command.Attributes.Count > 0)
            {
                var entities = command.Attributes
                    .Select(a => new OrderNoteAttribute { Name = a.Name, Value = a.Value })
                    .ToList();

                await _orders.AddNoteAttributesAsync(order.Id, entities, cancellationToken);
            }
        }
        else
        {
            _logger.LogWarning(
                "Order {OrderId} not found in local DB — note/attributes synced to Shopify but not persisted locally.",
                command.OrderId);
        }

        _logger.LogInformation(
            "Updated note/attributes for order {OrderGid}. Note={NoteSet}, Attributes={AttrCount}.",
            orderGid, command.Note is not null, command.Attributes.Count);

        var added = command.Attributes
            .Select(a => new NoteAttributeAdded(a.Name, a.Value))
            .ToList()
            .AsReadOnly();

        return new AddOrderNoteAttributesResult(
            OrderGid:       orderGid,
            Note:           command.Note,
            AddedAttributes: added);
    }
}
