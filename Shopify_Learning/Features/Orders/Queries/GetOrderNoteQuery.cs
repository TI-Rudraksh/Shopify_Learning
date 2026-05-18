using MediatR;

namespace ShopifyIntegration.Features.Orders.Queries;

public sealed record GetOrderNoteQuery(string OrderId)
    : IRequest<GetOrderNoteResult>;

public sealed record GetOrderNoteResult(
    string                          OrderGid,
    string?                         Note,
    IReadOnlyList<NoteAttributeDto> NoteAttributes);

public sealed record NoteAttributeDto(string Name, string Value);
