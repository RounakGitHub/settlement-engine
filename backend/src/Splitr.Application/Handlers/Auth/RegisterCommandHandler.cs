using Splitr.Application.Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Splitr.Application.Commands.Auth;
using Splitr.Application.Configuration;
using Splitr.Application.Helpers;
using Splitr.Application.Interfaces;
using Splitr.Domain.Entities;

namespace Splitr.Application.Handlers.Auth;

public class RegisterCommandHandler(
    IAppDbContext dbContext,
    IJwtTokenService jwtTokenService,
    IOptions<AuthOptions> authOptions) : IRequestHandler<RegisterCommand, AuthResult>
{
    private readonly AuthOptions _auth = authOptions.Value;

    public async Task<AuthResult> Handle(RegisterCommand request, CancellationToken cancellationToken)
    {
        var existing = await dbContext.Users
            .FirstOrDefaultAsync(u => u.Email == request.Email, cancellationToken);

        if (existing is not null)
        {
            if (existing.GoogleId is not null)
                throw new InvalidOperationException(
                    "An account with this email already exists via Google sign-in. Please use Google to sign in.");

            throw new InvalidOperationException("A user with this email already exists.");
        }

        var user = new User
        {
            Email = request.Email,
            Name = request.Name,
            PasswordHash = BCrypt.Net.BCrypt.EnhancedHashPassword(request.Password, _auth.BcryptCost)
        };

        dbContext.Users.Add(user);

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
