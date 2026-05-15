namespace ShopifyIntegration.GraphQL.Responses.Fulfillment;

public class FulfillmentCreateResponse
{
    public FulfillmentCreatePayload? FulfillmentCreate { get; set; }
}

public class FulfillmentCreatePayload
{
    public ShopifyFulfillmentNode?      Fulfillment { get; set; }
    public List<FulfillmentUserError>?  UserErrors  { get; set; }
}

public class ShopifyFulfillmentNode
{
    public string?                       Id           { get; set; }
    public string?                       Status       { get; set; }
    public string?                       CreatedAt    { get; set; }
    public string?                       UpdatedAt    { get; set; }
    public List<ShopifyTrackingInfo>?    TrackingInfo { get; set; }
}

public class ShopifyTrackingInfo
{
    public string? Number  { get; set; }
    public string? Company { get; set; }
    public string? Url     { get; set; }
}

public class FulfillmentUserError
{
    public List<string>? Field   { get; set; }
    public string?       Message { get; set; }
}
