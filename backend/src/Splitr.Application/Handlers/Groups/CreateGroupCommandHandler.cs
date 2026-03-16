using System.Security.Cryptography;
using Splitr.Application.Mediator;
using Microsoft.Extensions.Options;
using Splitr.Application.Commands.Groups;
using Splitr.Application.Configuration;
using Splitr.Application.Interfaces;
using Splitr.Domain.Entities;
using Splitr.Domain.Enums;

namespace Splitr.Application.Handlers.Groups;

public class CreateGroupCommandHandler(
    IAppDbContext dbContext,
    ICurrentUserService currentUser,
    IOptions<GroupOptions> groupOptions) : IRequestHandler<CreateGroupCommand, CreateGroupResult>
{
    private const string InviteCodeChars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    private readonly GroupOptions _options = groupOptions.Value;

    public async Task<CreateGroupResult> Handle(CreateGroupCommand request, CancellationToken cancellationToken)
    {
        var group = new Group
        {
            Name = request.Name,
            Currency = request.Currency,
            Category = request.Category,
            CreatedBy = currentUser.UserId,
            InviteCode = GenerateInviteCode()
        };

        dbContext.Groups.Add(group);

        dbContext.GroupMembers.Add(new GroupMember
        {
            GroupId = group.Id,
            UserId = currentUser.UserId,
            Role = GroupRole.Admin
        });

        dbContext.OutboxEvents.Add(OutboxEvent.From(EventType.GroupCreated, new { group.Id, group.Name }));

        await dbContext.SaveChangesAsync(cancellationToken);
        return new CreateGroupResult(group.Id, group.InviteCode);
    }

    private string GenerateInviteCode()
    {
        var length = _options.InviteCodeLength;
        var bytes = RandomNumberGenerator.GetBytes(length);
        return string.Create(length, bytes, (span, b) =>
        {
            for (var i = 0; i < span.Length; i++)
                span[i] = InviteCodeChars[b[i] % InviteCodeChars.Length];
        });
    }
}