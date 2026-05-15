namespace ShopifyIntegration.GraphQL.Responses.Inventory;

public class ActivateInventoryItemResponse
{
    public InventoryBulkToggleActivationPayload? InventoryBulkToggleActivation { get; set; }
}

public class InventoryBulkToggleActivationPayload
{
    public List<InventoryUserError>? UserErrors { get; set; }
}
