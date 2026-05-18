using MediatR;

namespace ShopifyIntegration.Features.Orders.Commands;

public sealed record AddOrderNoteAttributesCommand(
    string                            OrderId,
    string?                           Note,
    IReadOnlyList<NoteAttributeInput> Attributes)
    : IRequest<AddOrderNoteAttributesResult>;

public sealed record NoteAttributeInput(string Name, string Value);

public sealed record AddOrderNoteAttributesResult(
    string                            OrderGid,
    string?                           Note,
    IReadOnlyList<NoteAttributeAdded> AddedAttributes);

public sealed record NoteAttributeAdded(string Name, string Value);
