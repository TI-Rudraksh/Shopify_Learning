namespace ShopifyIntegration.Domain.Entities;

public sealed class Order
{
    public int            Id                { get; set; }       // PK, auto-increment
    public string         ShopifyGid        { get; set; } = ""; // unique, not null
    public long           NumericId         { get; set; }       // not null
    public string         Name              { get; set; } = ""; // e.g. #1001, not null
    public string         FinancialStatus   { get; set; } = ""; // not null
    public string         FulfillmentStatus { get; set; } = ""; // not null
    public decimal        TotalPrice        { get; set; }       // not null
    public string         Currency          { get; set; } = ""; // not null
    public string?        Note              { get; set; }       // nullable order note
    public int?           CustomerId        { get; set; }       // nullable FK → Customer.Id
    public Customer?      Customer          { get; set; }       // navigation property
    public DateTimeOffset CreatedAt         { get; set; }       // timestamptz, not null
    public DateTimeOffset UpdatedAt         { get; set; }       // timestamptz, not null
    public DateTimeOffset? CancelledAt      { get; set; }       // nullable

    public ICollection<OrderLineItem>      LineItems       { get; set; } = new List<OrderLineItem>();
    public ICollection<Fulfillment>        Fulfillments    { get; set; } = new List<Fulfillment>();
    public ICollection<OrderNoteAttribute> NoteAttributes  { get; set; } = new List<OrderNoteAttribute>();
}
