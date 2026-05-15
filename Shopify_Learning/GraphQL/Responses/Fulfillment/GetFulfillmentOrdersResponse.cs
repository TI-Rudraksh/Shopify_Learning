namespace ShopifyIntegration.GraphQL.Responses.Fulfillment;

public class GetFulfillmentOrdersResponse
{
    public ShopifyOrderNode? Order { get; set; }
}

public class ShopifyOrderNode
{
    public ShopifyFulfillmentOrderConnection? FulfillmentOrders { get; set; }
}

public class ShopifyFulfillmentOrderConnection
{
    public List<ShopifyFulfillmentOrderEdge>? Edges { get; set; }
}

public class ShopifyFulfillmentOrderEdge
{
    public ShopifyFulfillmentOrderNode? Node { get; set; }
}

public class ShopifyFulfillmentOrderNode
{
    public string? Id     { get; set; }
    public string? Status { get; set; }
}
