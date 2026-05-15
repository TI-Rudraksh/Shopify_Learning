namespace ShopifyIntegration.GraphQL.Responses.Inventory;

public class SetOnHandQuantityResponse
{
    public InventorySetOnHandPayload? InventorySetOnHandQuantities { get; set; }
}

public class InventorySetOnHandPayload
{
    public InventoryAdjustmentGroup? InventoryAdjustmentGroup { get; set; }
    public List<InventoryUserError>? UserErrors { get; set; }
}

public class InventoryAdjustmentGroup
{
    public string? Reason { get; set; }
    public List<InventoryChange>? Changes { get; set; }
}

public class InventoryChange
{
    public string? Name { get; set; }
    public int? Delta { get; set; }
    public int? QuantityAfterChange { get; set; }
    public ShopifyInventoryItemRef? Item { get; set; }
    public ShopifyLocationRef? Location { get; set; }
}

public class ShopifyLocationRef
{
    public string? Id { get; set; }
    public string? Name { get; set; }
}

public class InventoryUserError
{
    public List<string>? Field { get; set; }
    public string? Message { get; set; }
    public string? Code { get; set; }
}
