namespace ShopifyIntegration.GraphQL.Responses.Orders;

public class OrderUpdateResponse
{
    public OrderUpdatePayload? OrderUpdate { get; set; }
}

public class OrderUpdatePayload
{
    public ShopifyOrderNode?        Order      { get; set; }
    public List<OrderUserError>?    UserErrors { get; set; }
}

public class GetOrderNoteResponse
{
    public ShopifyOrderNode? Order { get; set; }
}

public class ShopifyOrderNode
{
    public string?                        Id               { get; set; }
    public string?                        Note             { get; set; }
    public List<ShopifyCustomAttribute>?  CustomAttributes { get; set; }
}

public class ShopifyCustomAttribute
{
    public string? Key   { get; set; }
    public string? Value { get; set; }
}

public class OrderUserError
{
    public List<string>? Field   { get; set; }
    public string?       Message { get; set; }
}
