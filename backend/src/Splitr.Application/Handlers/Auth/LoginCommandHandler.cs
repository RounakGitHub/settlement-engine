using System.Security.Cryptography;
using System.Text;
using Splitr.Application.Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Splitr.Application.Commands.Auth;
using Splitr.Application.Configuration;
using Splitr.Application.Helpers;
using Splitr.Application.Interfaces;
using Splitr.Domain.Entities;

namespace Splitr.Application.Handlers.Auth;

public class LoginCommandHandler(
    IAppDbContext dbContext,
    IJwtTokenService jwtTokenService,
    IOptions<AuthOptions> authOptions) : IRequestHandler<LoginCommand, AuthResult>
{
    private readonly AuthOptions _auth = authOptions.Value;

    public async Task<AuthResult> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users.FirstOrDefaultAsync(
            u => u.Email == request.Email,
            cancellationToken
        ) ?? throw new UnauthorizedAccessException("Invalid email or password.");

        if (user.LockedUntil > DateTime.UtcNow)
            throw new UnauthorizedAccessException("Account is temporarily locked. Try again later.");

        // Google-only users have no password — they must sign in via Google
        if (string.IsNullOrEmpty(user.PasswordHash))
            throw new UnauthorizedAccessException("This account uses Google sign-in. Please sign in with Google.");

        if (!VerifyPassword(request.Password, user.PasswordHash))
        {
            user.FailedLoginAttempts++;
            if (user.FailedLoginAttempts >= _auth.MaxFailedAttempts)
                user.LockedUntil = DateTime.UtcNow.AddMinutes(_auth.LockoutMinutes);

            await dbContext.SaveChangesAsync(cancellationToken);
            throw new UnauthorizedAccessException("Invalid email or password.");
        }

        // Transparent BCrypt migration for old PBKDF2 hashes
        if (user.PasswordHash.Contains('.'))
        {
            user.PasswordHash = BCrypt.Net.BCrypt.EnhancedHashPassword(request.Password, _auth.BcryptCost);
        }

        user.FailedLoginAttempts = 0;
        user.LockedUntil = null;

        await dbContext.RefreshTokens
            .Where(rt => rt.UserId == user.Id && rt.RevokedAt == null)
            .ExecuteUpdateAsync(
                rt => rt.SetProperty(t => t.RevokedAt, DateTime.UtcNow),
                cancellationToken
            );

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

    private static bool VerifyPassword(string password, string storedHash)
    {
        // PBKDF2 legacy format: base64salt.base64hash
        if (storedHash.Contains('.'))
        {
            var parts = storedHash.Split('.');
            if (parts.Length != 2)
                return false;

            var salt = Convert.FromBase64String(parts[0]);
            var hash = Convert.FromBase64String(parts[1]);
            var computedHash = Rfc2898DeriveBytes.Pbkdf2(
                Encoding.UTF8.GetBytes(password), salt, 100_000, HashAlgorithmName.SHA256, 32
            );

            return CryptographicOperations.FixedTimeEquals(hash, computedHash);
        }

        // BCrypt format
        return BCrypt.Net.BCrypt.EnhancedVerify(password, storedHash);
    }
}