namespace ShopifyIntegration.GraphQL.Responses.Orders;

public class OrderSyncResponse
{
    public OrderSyncConnection? Orders { get; set; }
}

public class OrderSyncConnection
{
    public OrderSyncPageInfo?        PageInfo { get; set; }
    public List<OrderSyncEdge>?      Edges    { get; set; }
}

public class OrderSyncPageInfo
{
    public bool    HasNextPage { get; set; }
    public string? EndCursor   { get; set; }
}

public class OrderSyncEdge
{
    public OrderSyncNode? Node { get; set; }
}

public class OrderSyncNode
{
    public string?                       Id                        { get; set; }
    public string?                       Name                      { get; set; }
    public string?                       DisplayFinancialStatus    { get; set; }
    public string?                       DisplayFulfillmentStatus  { get; set; }
    public OrderSyncMoneySet?            TotalPriceSet             { get; set; }
    public string?                       Note                      { get; set; }
    public List<ShopifyCustomAttribute>? CustomAttributes          { get; set; }
    public DateTimeOffset                CreatedAt                 { get; set; }
    public DateTimeOffset                UpdatedAt                 { get; set; }
    public DateTimeOffset?               CancelledAt               { get; set; }
    public OrderSyncCustomerRef?         Customer                  { get; set; }
    public OrderSyncLineItemConnection?  LineItems                 { get; set; }
}

public class OrderSyncMoneySet
{
    public OrderSyncMoney? ShopMoney { get; set; }
}

public class OrderSyncMoney
{
    public string? Amount       { get; set; }
    public string? CurrencyCode { get; set; }
}

public class OrderSyncCustomerRef
{
    public string? Id { get; set; }
}

public class OrderSyncLineItemConnection
{
    public List<OrderSyncLineItemEdge>? Edges { get; set; }
}

public class OrderSyncLineItemEdge
{
    public OrderSyncLineItemNode? Node { get; set; }
}

public class OrderSyncLineItemNode
{
    public string?              Id                      { get; set; }
    public string?              Title                   { get; set; }
    public string?              VariantTitle            { get; set; }
    public int                  Quantity                { get; set; }
    public OrderSyncMoneySet?   OriginalUnitPriceSet    { get; set; }
    public string?              Sku                     { get; set; }
    public OrderSyncProductRef? Product                 { get; set; }
    public OrderSyncVariantRef? Variant                 { get; set; }
}

public class OrderSyncProductRef
{
    public string? Id { get; set; }
}

public class OrderSyncVariantRef
{
    public string? Id { get; set; }
}
