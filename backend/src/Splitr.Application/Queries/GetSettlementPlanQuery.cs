using Splitr.Application.Mediator;
using Splitr.Application.Behaviours;
using Splitr.Domain.Algorithms;

namespace Splitr.Application.Queries;

public record GetSettlementPlanQuery(Guid GroupId) : IRequest<List<Transfer>>, IRequireGroupMembership;
