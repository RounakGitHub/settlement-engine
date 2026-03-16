using Splitr.Application.Mediator;
using Splitr.Application.Behaviours;

namespace Splitr.Application.Queries;

public record UserBalance(Guid UserId, string UserName, long NetBalancePaise);

public record GetGroupBalancesQuery(Guid GroupId) : IRequest<List<UserBalance>>, IRequireGroupMembership;
