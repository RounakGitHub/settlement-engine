using Splitr.Application.Mediator;
using Splitr.Application.Behaviours;

namespace Splitr.Application.Commands.Groups;

public record RegenerateInviteCodeResult(string InviteCode);

public record RegenerateInviteCodeCommand(Guid GroupId) : IRequest<RegenerateInviteCodeResult>, IRequireGroupMembership;
