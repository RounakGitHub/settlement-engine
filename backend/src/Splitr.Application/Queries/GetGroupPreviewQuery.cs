using Splitr.Application.Mediator;

namespace Splitr.Application.Queries;

public record GroupPreviewResult(Guid GroupId, string Name, int MemberCount, string Currency);

public record GetGroupPreviewQuery(string InviteCode) : IRequest<GroupPreviewResult>;
