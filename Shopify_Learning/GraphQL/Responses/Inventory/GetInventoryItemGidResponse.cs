namespace ShopifyIntegration.GraphQL.Responses.Inventory;

public class GetInventoryItemGidResponse
{
    public ShopifyProductVariants? Product { get; set; }
}

public class ShopifyProductVariants
{
    public ShopifyVariantConnection? Variants { get; set; }
}

public class ShopifyVariantConnection
{
    public List<ShopifyVariantEdge>? Edges { get; set; }
}

public class ShopifyVariantEdge
{
    public ShopifyVariantNode? Node { get; set; }
}

public class ShopifyVariantNode
{
    public ShopifyInventoryItemRef? InventoryItem { get; set; }
}

public class ShopifyInventoryItemRef
{
    public string? Id { get; set; }
}
