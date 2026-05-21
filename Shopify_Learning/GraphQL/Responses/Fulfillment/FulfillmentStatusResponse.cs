namespace ShopifyIntegration.GraphQL.Responses.Fulfillment;

public class FulfillmentStatusResponse
{
    public FulfillmentStatusNode? Fulfillment { get; set; }
}

public class FulfillmentStatusNode
{
    public string?         Id        { get; set; }
    public string?         Status    { get; set; }
    public DateTimeOffset  UpdatedAt { get; set; }
}
