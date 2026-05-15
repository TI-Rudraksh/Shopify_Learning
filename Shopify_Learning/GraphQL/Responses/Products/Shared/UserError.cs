namespace ShopifyIntegration.GraphQL.Responses.Products.Shared;

public class UserError
{
    public List<string>? Field { get; set; }

    public string? Message { get; set; }
}