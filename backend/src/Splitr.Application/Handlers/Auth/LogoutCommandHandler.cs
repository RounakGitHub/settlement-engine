using Splitr.Application.Mediator;
using Microsoft.EntityFrameworkCore;
using Splitr.Application.Commands.Auth;
using Splitr.Application.Interfaces;

namespace Splitr.Application.Handlers.Auth;

public class LogoutCommandHandler(IAppDbContext dbContext, ICurrentUserService currentUser) : IRequestHandler<LogoutCommand, Unit>
{
    public async Task<Unit> Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        await dbContext.RefreshTokens
            .Where(rt => rt.UserId == currentUser.UserId && rt.RevokedAt == null)
            .ExecuteUpdateAsync(
                rt => rt.SetProperty(t => t.RevokedAt, DateTime.UtcNow),
                cancellationToken
            );

        return Unit.Value;
    }
}
