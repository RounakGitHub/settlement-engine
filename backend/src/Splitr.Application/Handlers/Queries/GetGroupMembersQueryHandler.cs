using Splitr.Application.Mediator;
using Microsoft.EntityFrameworkCore;
using Splitr.Application.Interfaces;
using Splitr.Application.Queries;

namespace Splitr.Application.Handlers.Queries;

public class GetGroupMembersQueryHandler(IAppDbContext dbContext) : IRequestHandler<GetGroupMembersQuery, List<GroupMemberDto>>
{
    public async Task<List<GroupMemberDto>> Handle(GetGroupMembersQuery request, CancellationToken cancellationToken)
    {
        return await dbContext.GroupMembers
            .Where(gm => gm.GroupId == request.GroupId)
            .Join(dbContext.Users, gm => gm.UserId, u => u.Id, (gm, u) => new { gm, u })
            .OrderBy(x => x.gm.JoinedAt)
            .Select(x => new GroupMemberDto(
                x.gm.UserId,
                x.u.Name,
                x.u.Email,
                x.gm.Role.ToString(),
                x.gm.JoinedAt
            ))
            .ToListAsync(cancellationToken);
    }
}
