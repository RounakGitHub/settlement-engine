using Splitr.Application.Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Splitr.Application.Commands.Auth;
using Splitr.Application.Configuration;
using Splitr.Application.Interfaces;
using Splitr.Domain.Entities;
using Splitr.Domain.Enums;

namespace Splitr.Application.Handlers.Auth;

public class DeleteAccountCommandHandler(
    IAppDbContext dbContext,
    ICurrentUserService currentUser,
    IOptions<GroupOptions> groupOptions) : IRequestHandler<DeleteAccountCommand, Unit>
{
    private readonly GroupOptions _groupOptions = groupOptions.Value;

    public async Task<Unit> Handle(DeleteAccountCommand request, CancellationToken ct)
    {
        var userId = currentUser.UserId;

        var user = await dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw new KeyNotFoundException("User not found.");

        // 1. Cancel all pending settlements where user is payer or payee
        await dbContext.Settlements
            .Where(s => (s.PayerId == userId || s.PayeeId == userId)
                        && s.Status == SettlementStatus.Pending)
            .ExecuteUpdateAsync(
                s => s.SetProperty(x => x.Status, SettlementStatus.Cancelled),
                ct);

        // 2. Handle group memberships
        var memberships = await dbContext.GroupMembers
            .Where(gm => gm.UserId == userId)
            .ToListAsync(ct);

        foreach (var membership in memberships)
        {
            var groupId = membership.GroupId;

            if (membership.Role == GroupRole.Admin)
            {
                // Try to promote another member to admin
                var nextMember = await dbContext.GroupMembers
                    .Where(gm => gm.GroupId == groupId && gm.UserId != userId)
                    .OrderBy(gm => gm.JoinedAt)
                    .FirstOrDefaultAsync(ct);

                if (nextMember is not null)
                {
                    nextMember.Role = GroupRole.Admin;
                }
                else
                {
                    // Sole member — archive the group
                    var group = await dbContext.Groups.FirstAsync(g => g.Id == groupId, ct);
                    group.IsArchived = true;
                    group.ArchivedAt = DateTime.UtcNow;
                    group.DeleteAfter = DateTime.UtcNow.AddDays(_groupOptions.ArchiveRetentionDays);
                    dbContext.OutboxEvents.Add(OutboxEvent.From(EventType.GroupArchived, new { GroupId = groupId }));
                }
            }
            else
            {
                // Regular member — check if group becomes empty after leaving
                var remainingCount = await dbContext.GroupMembers
                    .CountAsync(gm => gm.GroupId == groupId && gm.UserId != userId, ct);

                if (remainingCount == 0)
                {
                    var group = await dbContext.Groups.FirstAsync(g => g.Id == groupId, ct);
                    group.IsArchived = true;
                    group.ArchivedAt = DateTime.UtcNow;
                    group.DeleteAfter = DateTime.UtcNow.AddDays(_groupOptions.ArchiveRetentionDays);
                    dbContext.OutboxEvents.Add(OutboxEvent.From(EventType.GroupArchived, new { GroupId = groupId }));
                }
            }

            dbContext.GroupMembers.Remove(membership);
            dbContext.OutboxEvents.Add(OutboxEvent.From(EventType.MemberLeft, new { GroupId = groupId, UserId = userId }));
        }

        // 3. Revoke all refresh tokens
        await dbContext.RefreshTokens
            .Where(rt => rt.UserId == userId && rt.RevokedAt == null)
            .ExecuteUpdateAsync(
                rt => rt.SetProperty(t => t.RevokedAt, DateTime.UtcNow),
                ct);

        // 4. Anonymize user data (can't hard-delete due to FK Restrict on expenses/splits/settlements)
        user.Name = "Deleted User";
        user.Email = $"deleted_{userId:N}@splitr.local";
        user.PasswordHash = null;
        user.GoogleId = null;
        user.FailedLoginAttempts = 0;
        user.LockedUntil = null;

        await dbContext.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
