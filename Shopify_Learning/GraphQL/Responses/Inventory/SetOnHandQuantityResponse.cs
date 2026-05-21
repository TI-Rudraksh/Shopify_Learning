using ShopifyIntegration.GraphQL.Responses.Shared;

namespace ShopifyIntegration.GraphQL.Responses.Inventory;

public class SetOnHandQuantityResponse
{
    public InventorySetOnHandPayload? InventorySetOnHandQuantities { get; set; }
}

public class InventorySetOnHandPayload
{
    public InventoryAdjustmentGroup? InventoryAdjustmentGroup { get; set; }
    public List<GraphQLUserError>?   UserErrors               { get; set; }
}

public class InventoryAdjustmentGroup
{
    public string?                Reason  { get; set; }
    public List<InventoryChange>? Changes { get; set; }
}

public class InventoryChange
{
    public string?                  Name                { get; set; }
    public int?                     Delta               { get; set; }
    public int?                     QuantityAfterChange { get; set; }
    public ShopifyInventoryItemRef? Item                { get; set; }
    public ShopifyLocationRef?      Location            { get; set; }
}

public class ShopifyLocationRef
{
    public string? Id   { get; set; }
    public string? Name { get; set; }
}
