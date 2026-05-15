namespace ShopifyIntegration.DTOs;

public sealed class UpdateInventoryRequest
{
    public int  Quantity  { get; set; }
    public bool Available { get; set; } = true;
}
