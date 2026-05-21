namespace ShopifyIntegration.GraphQL.Responses.Shared;

/// <summary>
/// Standard Shopify GraphQL userError shape returned by all mutations.
/// Replaces the ShopifySharp.GraphQL.UserError dependency so the project
/// has zero coupling to ShopifySharp's internal GraphQL type system.
/// </summary>
public sealed class GraphQLUserError
{
    public List<string>? Field   { get; set; }
    public string?       Message { get; set; }
    public string?       Code    { get; set; }
}
