using Splitr.Application.Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Splitr.Application.Commands.Groups;
using Splitr.Application.Configuration;
using Splitr.Application.Interfaces;
using Splitr.Domain.Entities;
using Splitr.Domain.Enums;

namespace Splitr.Application.Handlers.Groups;

public class LeaveGroupCommandHandler(
    IAppDbContext dbContext,
    ICurrentUserService currentUser,
    IOptions<GroupOptions> groupOptions) : IRequestHandler<LeaveGroupCommand, Unit>
{
    private readonly GroupOptions _options = groupOptions.Value;

    public async Task<Unit> Handle(LeaveGroupCommand request, CancellationToken cancellationToken)
    {
        var membership = await dbContext.GroupMembers.FirstAsync(
            gm => gm.GroupId == request.GroupId && gm.UserId == currentUser.UserId,
            cancellationToken
        );

        if (membership.Role == GroupRole.Admin)
        {
            var otherAdminExists = await dbContext.GroupMembers.AnyAsync(
                gm => gm.GroupId == request.GroupId
                       && gm.UserId != currentUser.UserId
                       && gm.Role == GroupRole.Admin,
                cancellationToken
            );

            if (!otherAdminExists)
                throw new InvalidOperationException("Promote another member to admin before leaving.");
        }

        dbContext.GroupMembers.Remove(membership);

        var remainingMembers = await dbContext.GroupMembers.CountAsync(
            gm => gm.GroupId == request.GroupId && gm.UserId != currentUser.UserId,
            cancellationToken
        );

        if (remainingMembers == 0)
        {
            var group = await dbContext.Groups.FirstAsync(g => g.Id == request.GroupId, cancellationToken);
            group.IsArchived = true;
            group.ArchivedAt = DateTime.UtcNow;
            group.DeleteAfter = DateTime.UtcNow.AddDays(_options.ArchiveRetentionDays);
            dbContext.OutboxEvents.Add(OutboxEvent.From(EventType.GroupArchived, new { GroupId = request.GroupId }));
        }

        dbContext.OutboxEvents.Add(OutboxEvent.From(EventType.MemberLeft, new { GroupId = request.GroupId, UserId = currentUser.UserId }));

        await dbContext.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}