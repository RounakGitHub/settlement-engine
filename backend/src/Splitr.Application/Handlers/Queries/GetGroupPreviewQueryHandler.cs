using Splitr.Application.Mediator;
using Microsoft.EntityFrameworkCore;
using Splitr.Application.Interfaces;
using Splitr.Application.Queries;

namespace Splitr.Application.Handlers.Queries;

public class GetGroupPreviewQueryHandler(IAppDbContext dbContext) : IRequestHandler<GetGroupPreviewQuery, GroupPreviewResult>
{
    public async Task<GroupPreviewResult> Handle(GetGroupPreviewQuery request, CancellationToken cancellationToken)
    {
        var group = await dbContext.Groups.FirstOrDefaultAsync(
            g => g.InviteCode == request.InviteCode && !g.IsArchived,
            cancellationToken
        ) ?? throw new InvalidOperationException("Invalid or expired invite code.");

        var memberCount = await dbContext.GroupMembers.CountAsync(
            gm => gm.GroupId == group.Id,
            cancellationToken
        );

        return new GroupPreviewResult(group.Id, group.Name, memberCount, group.Currency);
    }
}
