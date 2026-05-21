namespace ShopifyIntegration.GraphQL.Responses.Inventory;

public class FullInventorySyncResponse
{
    public FullInventorySyncProductConnection? Products { get; set; }
}

public class FullInventorySyncProductConnection
{
    public FullInventorySyncPageInfo?        PageInfo { get; set; }
    public List<FullInventorySyncProductEdge>? Edges  { get; set; }
}

public class FullInventorySyncPageInfo
{
    public bool    HasNextPage { get; set; }
    public string? EndCursor   { get; set; }
}

public class FullInventorySyncProductEdge
{
    public FullInventorySyncProductNode? Node { get; set; }
}

public class FullInventorySyncProductNode
{
    public string?                              Id       { get; set; }
    public FullInventorySyncVariantConnection?  Variants { get; set; }
}

public class FullInventorySyncVariantConnection
{
    public List<FullInventorySyncVariantEdge>? Edges { get; set; }
}

public class FullInventorySyncVariantEdge
{
    public FullInventorySyncVariantNode? Node { get; set; }
}

public class FullInventorySyncVariantNode
{
    public FullInventorySyncInventoryItem? InventoryItem { get; set; }
}

public class FullInventorySyncInventoryItem
{
    public string?                                  Id              { get; set; }
    public FullInventorySyncInventoryLevelConnection? InventoryLevels { get; set; }
}

public class FullInventorySyncInventoryLevelConnection
{
    public List<FullInventorySyncInventoryLevelEdge>? Edges { get; set; }
}

public class FullInventorySyncInventoryLevelEdge
{
    public FullInventorySyncInventoryLevelNode? Node { get; set; }
}

public class FullInventorySyncInventoryLevelNode
{
    public FullInventorySyncLocationRef?         Location   { get; set; }
    public List<FullInventorySyncQuantityEntry>? Quantities { get; set; }
}

public class FullInventorySyncLocationRef
{
    public string? Id { get; set; }
}

public class FullInventorySyncQuantityEntry
{
    public string? Name     { get; set; }
    public int     Quantity { get; set; }
}
