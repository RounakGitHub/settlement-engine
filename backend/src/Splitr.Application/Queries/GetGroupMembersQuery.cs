using Splitr.Application.Mediator;
using Splitr.Application.Behaviours;

namespace Splitr.Application.Queries;

public record GroupMemberDto(Guid UserId, string Name, string Email, string Role, DateTime JoinedAt);

public record GetGroupMembersQuery(Guid GroupId) : IRequest<List<GroupMemberDto>>, IRequireGroupMembership;
