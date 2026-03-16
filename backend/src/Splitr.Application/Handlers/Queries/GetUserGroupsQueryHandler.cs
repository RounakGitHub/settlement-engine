using Splitr.Application.Mediator;
using Microsoft.EntityFrameworkCore;
using Splitr.Application.Interfaces;
using Splitr.Application.Queries;

namespace Splitr.Application.Handlers.Queries;

public class GetUserGroupsQueryHandler(IAppDbContext dbContext, ICurrentUserService currentUser) : IRequestHandler<GetUserGroupsQuery, List<GroupDto>>
{
    public async Task<List<GroupDto>> Handle(GetUserGroupsQuery request, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId;

        var myGroupIds = dbContext.GroupMembers
            .Where(gm => gm.UserId == userId)
            .Select(gm => gm.GroupId);

        return await dbContext.Groups
            .Where(g => myGroupIds.Contains(g.Id) && !g.IsArchived)
            .OrderByDescending(g => g.CreatedAt)
            .Select(g => new GroupDto(
                g.Id,
                g.Name,
                g.Currency,
                g.Category,
                g.InviteCode,
                g.CreatedBy,
                g.IsArchived,
                g.CreatedAt,
                dbContext.GroupMembers.Count(gm => gm.GroupId == g.Id)
            ))
            .ToListAsync(cancellationToken);
    }
}
