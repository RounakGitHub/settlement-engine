using Splitr.Application.Mediator;

namespace Splitr.Application.Commands.Groups;

public record JoinGroupCommand(string InviteCode) : IRequest<Unit>;
