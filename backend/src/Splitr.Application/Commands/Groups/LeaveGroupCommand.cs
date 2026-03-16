using Splitr.Application.Mediator;
using Splitr.Application.Behaviours;

namespace Splitr.Application.Commands.Groups;

public record LeaveGroupCommand(Guid GroupId) : IRequest<Unit>, IRequireGroupMembership;
