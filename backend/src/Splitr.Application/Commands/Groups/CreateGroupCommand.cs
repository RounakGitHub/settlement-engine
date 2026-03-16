using Splitr.Application.Mediator;

namespace Splitr.Application.Commands.Groups;

public record CreateGroupResult(Guid GroupId, string InviteCode);

public record CreateGroupCommand(string Name, string Currency, string? Category) : IRequest<CreateGroupResult>;
