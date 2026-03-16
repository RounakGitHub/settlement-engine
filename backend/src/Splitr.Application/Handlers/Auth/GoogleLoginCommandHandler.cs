using Splitr.Application.Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Splitr.Application.Commands.Auth;
using Splitr.Application.Configuration;
using Splitr.Application.Helpers;
using Splitr.Application.Interfaces;
using Splitr.Domain.Entities;

namespace Splitr.Application.Handlers.Auth;

public class GoogleLoginCommandHandler(
    IAppDbContext dbContext,
    IJwtTokenService jwtTokenService,
    IOptions<AuthOptions> authOptions) : IRequestHandler<GoogleLoginCommand, AuthResult>
{
    private readonly AuthOptions _auth = authOptions.Value;

    public async Task<AuthResult> Handle(GoogleLoginCommand request, CancellationToken cancellationToken)
    {
        // Try to find user by GoogleId first, then by email
        var user = await dbContext.Users
            .FirstOrDefaultAsync(u => u.GoogleId == request.GoogleId, cancellationToken);

        if (user is null)
        {
            // Check if a user with this email exists (registered via email/password)
            user = await dbContext.Users
                .FirstOrDefaultAsync(u => u.Email == request.Email, cancellationToken);

            if (user is not null)
            {
                // Link Google account to existing user
                user.GoogleId = request.GoogleId;
                user.FailedLoginAttempts = 0;
                user.LockedUntil = null;
            }
            else
            {
                // Create new user from Google profile
                user = new User
                {
                    Email = request.Email,
                    Name = request.Name,
                    GoogleId = request.GoogleId,
                    PasswordHash = null
                };
                dbContext.Users.Add(user);
            }
        }

        // Revoke existing refresh tokens (new session)
        var existingTokens = await dbContext.RefreshTokens
            .Where(rt => rt.UserId == user.Id && rt.RevokedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var token in existingTokens)
            token.RevokedAt = DateTime.UtcNow;

        // Create new refresh token
        var tokenFamily = Guid.NewGuid();
        var refreshToken = jwtTokenService.GenerateRefreshToken();
        dbContext.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = TokenHasher.Hash(refreshToken),
            ExpiresAt = DateTime.UtcNow.AddDays(_auth.RefreshTokenExpiryDays),
            TokenFamily = tokenFamily
        });

        await dbContext.SaveChangesAsync(cancellationToken);

        var accessToken = jwtTokenService.GenerateAccessToken(user);
        return new AuthResult(accessToken, refreshToken, user.Id, user.Name);
    }
}
