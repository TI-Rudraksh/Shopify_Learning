namespace ShopifyIntegration.GraphQL.Responses.Fulfillment;

public class GetFulfillmentOrdersWithLineItemsResponse
{
    public ShopifyOrderWithLineItemsNode? Order { get; set; }
}

public class ShopifyOrderWithLineItemsNode
{
    public ShopifyFulfillmentOrderWithLineItemsConnection? FulfillmentOrders { get; set; }
}

public class ShopifyFulfillmentOrderWithLineItemsConnection
{
    public List<ShopifyFulfillmentOrderWithLineItemsEdge>? Edges { get; set; }
}

public class ShopifyFulfillmentOrderWithLineItemsEdge
{
    public ShopifyFulfillmentOrderWithLineItemsNode? Node { get; set; }
}

public class ShopifyFulfillmentOrderWithLineItemsNode
{
    public string?                                    Id        { get; set; }
    public string?                                    Status    { get; set; }
    public ShopifyFulfillmentOrderLineItemConnection? LineItems { get; set; }
}

public class ShopifyFulfillmentOrderLineItemConnection
{
    public List<ShopifyFulfillmentOrderLineItemEdge>? Edges { get; set; }
}

public class ShopifyFulfillmentOrderLineItemEdge
{
    public ShopifyFulfillmentOrderLineItemNode? Node { get; set; }
}

public class ShopifyFulfillmentOrderLineItemNode
{
    public string?                   Id                { get; set; }  // FulfillmentOrderLineItem GID
    public int                       RemainingQuantity { get; set; }  // units still to fulfill
    public ShopifyLineItemReference? LineItem          { get; set; }
}

public class ShopifyLineItemReference
{
    public string? Id { get; set; }  // OrderLineItem GID (matches OrderLineItem.ShopifyGid)
}
