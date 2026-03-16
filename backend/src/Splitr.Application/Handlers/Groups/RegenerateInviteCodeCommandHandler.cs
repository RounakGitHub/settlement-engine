using System.Security.Cryptography;
using Splitr.Application.Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Splitr.Application.Commands.Groups;
using Splitr.Application.Configuration;
using Splitr.Application.Exceptions;
using Splitr.Application.Interfaces;
using Splitr.Domain.Enums;

namespace Splitr.Application.Handlers.Groups;

public class RegenerateInviteCodeCommandHandler(
    IAppDbContext dbContext,
    ICurrentUserService currentUser,
    IOptions<GroupOptions> groupOptions) : IRequestHandler<RegenerateInviteCodeCommand, RegenerateInviteCodeResult>
{
    private const string InviteCodeChars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    private readonly GroupOptions _options = groupOptions.Value;

    public async Task<RegenerateInviteCodeResult> Handle(RegenerateInviteCodeCommand request, CancellationToken cancellationToken)
    {
        var isAdmin = await dbContext.GroupMembers.AnyAsync(
            gm => gm.GroupId == request.GroupId && gm.UserId == currentUser.UserId && gm.Role == GroupRole.Admin,
            cancellationToken
        );

        if (!isAdmin)
            throw new ForbiddenAccessException("Only group admins can regenerate invite codes.");

        var group = await dbContext.Groups.FirstAsync(g => g.Id == request.GroupId, cancellationToken);
        group.InviteCode = GenerateInviteCode();

        await dbContext.SaveChangesAsync(cancellationToken);
        return new RegenerateInviteCodeResult(group.InviteCode);
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