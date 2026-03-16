using Splitr.Application.Mediator;
using Splitr.Application.Behaviours;

namespace Splitr.Application.Commands.Groups;

public record DeleteGroupCommand(Guid GroupId) : IRequest<Unit>, IRequireGroupMembership;
