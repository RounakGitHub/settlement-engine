using Splitr.Application.Mediator;

namespace Splitr.Application.Queries;

public record GroupDto(Guid Id, string Name, string Currency, string? Category, string? InviteCode, Guid CreatedBy, bool IsArchived, DateTime CreatedAt, int MemberCount);

public record GetUserGroupsQuery : IRequest<List<GroupDto>>;
