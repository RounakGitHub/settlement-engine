using Splitr.Application.Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Splitr.Application.Commands.Auth;
using Splitr.Application.Configuration;
using Splitr.Application.Helpers;
using Splitr.Application.Interfaces;
using Splitr.Domain.Entities;

namespace Splitr.Application.Handlers.Auth;

public class RefreshTokenCommandHandler(
    IAppDbContext dbContext,
    IJwtTokenService jwtTokenService,
    IOptions<AuthOptions> authOptions) : IRequestHandler<RefreshTokenCommand, AuthResult>
{
    private readonly AuthOptions _auth = authOptions.Value;

    public async Task<AuthResult> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        var tokenHash = TokenHasher.Hash(request.RefreshToken);

        // Look up token INCLUDING revoked ones for reuse detection
        var storedToken = await dbContext.RefreshTokens
            .FirstOrDefaultAsync(
                rt => rt.TokenHash == tokenHash,
                cancellationToken
            )
            ?? throw new UnauthorizedAccessException("Invalid or expired refresh token.");

        // Reuse detection: if this token was already rotated, someone stole it
        if (storedToken.RevokedAt is not null)
        {
            // Revoke ALL tokens in this family
            await dbContext.RefreshTokens
                .Where(rt => rt.TokenFamily == storedToken.TokenFamily)
                .ExecuteUpdateAsync(
                    rt => rt.SetProperty(t => t.RevokedAt, DateTime.UtcNow),
                    cancellationToken
                );

            throw new UnauthorizedAccessException("Token reuse detected. All sessions revoked.");
        }

        if (storedToken.ExpiresAt <= DateTime.UtcNow)
            throw new UnauthorizedAccessException("Refresh token has expired.");

        var user = await dbContext.Users.FirstAsync(u => u.Id == storedToken.UserId, cancellationToken);

        var newRefreshToken = jwtTokenService.GenerateRefreshToken();
        var newTokenHash = TokenHasher.Hash(newRefreshToken);

        // Rotate: revoke old, point to successor
        storedToken.RevokedAt = DateTime.UtcNow;
        storedToken.ReplacedByTokenHash = newTokenHash;

        dbContext.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = newTokenHash,
            ExpiresAt = DateTime.UtcNow.AddDays(_auth.RefreshTokenExpiryDays),
            TokenFamily = storedToken.TokenFamily
        });

        await dbContext.SaveChangesAsync(cancellationToken);

        var accessToken = jwtTokenService.GenerateAccessToken(user);
        return new AuthResult(accessToken, newRefreshToken, user.Id, user.Name);
    }
}