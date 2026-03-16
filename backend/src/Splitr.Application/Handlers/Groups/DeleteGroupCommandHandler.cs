using Splitr.Application.Mediator;
using Microsoft.EntityFrameworkCore;
using Splitr.Application.Commands.Groups;
using Splitr.Application.Exceptions;
using Splitr.Application.Interfaces;
using Splitr.Domain.Entities;
using Splitr.Domain.Enums;

namespace Splitr.Application.Handlers.Groups;

public class DeleteGroupCommandHandler(
    IAppDbContext dbContext,
    ICurrentUserService currentUser) : IRequestHandler<DeleteGroupCommand, Unit>
{
    public async Task<Unit> Handle(DeleteGroupCommand request, CancellationToken cancellationToken)
    {
        var isAdmin = await dbContext.GroupMembers.AnyAsync(
            gm => gm.GroupId == request.GroupId && gm.UserId == currentUser.UserId && gm.Role == GroupRole.Admin,
            cancellationToken
        );

        if (!isAdmin)
            throw new ForbiddenAccessException("Only a group admin can delete this group.");

        var group = await dbContext.Groups.FirstAsync(
            g => g.Id == request.GroupId && !g.IsArchived,
            cancellationToken
        );

        // Cancel any pending settlements
        await dbContext.Settlements
            .Where(s => s.GroupId == request.GroupId && s.Status == SettlementStatus.Pending)
            .ExecuteUpdateAsync(
                s => s.SetProperty(x => x.Status, SettlementStatus.Cancelled),
                cancellationToken
            );

        // Archive the group (soft delete)
        group.IsArchived = true;
        group.ArchivedAt = DateTime.UtcNow;
        group.DeleteAfter = DateTime.UtcNow.AddDays(30);

        dbContext.OutboxEvents.Add(OutboxEvent.From(EventType.GroupArchived, new { GroupId = request.GroupId, DeletedBy = currentUser.UserId }));

        await dbContext.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
