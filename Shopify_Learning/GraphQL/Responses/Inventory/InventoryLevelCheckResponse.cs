namespace ShopifyIntegration.GraphQL.Responses.Inventory;

public class InventoryLevelCheckResponse
{
    public List<InventoryItemNode?>? Nodes { get; set; }
}

public class InventoryItemNode
{
    public string?                          Id              { get; set; }
    public InventoryLevelCheckConnection?   InventoryLevels { get; set; }
}

public class InventoryLevelCheckConnection
{
    public List<InventoryLevelCheckEdge>? Edges { get; set; }
}

public class InventoryLevelCheckEdge
{
    public InventoryLevelCheckNode? Node { get; set; }
}

public class InventoryLevelCheckNode
{
    public InventoryLocationRef?          Location   { get; set; }
    public List<InventoryQuantityEntry>?  Quantities { get; set; }
}

public class InventoryLocationRef
{
    public string? Id { get; set; }
}

public class InventoryQuantityEntry
{
    public string? Name     { get; set; }
    public int     Quantity { get; set; }
}
