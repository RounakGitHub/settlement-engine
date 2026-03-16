using Splitr.Application.Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Splitr.Application.Commands.Groups;
using Splitr.Application.Configuration;
using Splitr.Application.Interfaces;
using Splitr.Domain.Entities;
using Splitr.Domain.Enums;

namespace Splitr.Application.Handlers.Groups;

public class JoinGroupCommandHandler(
    IAppDbContext dbContext,
    ICurrentUserService currentUser,
    IOptions<GroupOptions> groupOptions) : IRequestHandler<JoinGroupCommand, Unit>
{
    private readonly GroupOptions _options = groupOptions.Value;

    public async Task<Unit> Handle(JoinGroupCommand request, CancellationToken cancellationToken)
    {
        var group = await dbContext.Groups.FirstOrDefaultAsync(
            g => g.InviteCode == request.InviteCode && !g.IsArchived,
            cancellationToken
        ) ?? throw new InvalidOperationException("Invalid or expired invite code.");

        var alreadyMember = await dbContext.GroupMembers.AnyAsync(
            gm => gm.GroupId == group.Id && gm.UserId == currentUser.UserId,
            cancellationToken
        );

        if (alreadyMember)
            return Unit.Value;

        var memberCount = await dbContext.GroupMembers.CountAsync(
            gm => gm.GroupId == group.Id,
            cancellationToken
        );

        if (memberCount >= _options.MaxMembers)
            throw new InvalidOperationException($"Group has reached the maximum of {_options.MaxMembers} members.");

        dbContext.GroupMembers.Add(new GroupMember
        {
            GroupId = group.Id,
            UserId = currentUser.UserId,
            Role = GroupRole.Member
        });

        dbContext.OutboxEvents.Add(OutboxEvent.From(EventType.MemberJoined, new { GroupId = group.Id, UserId = currentUser.UserId }));

        await dbContext.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}