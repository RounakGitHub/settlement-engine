using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Splitr.Application.Interfaces;
using Splitr.Domain.Entities;
using Splitr.Infrastructure.Configuration;

namespace Splitr.Infrastructure.Services;

public class JwtTokenService(IOptions<JwtOptions> jwtOptions) : IJwtTokenService
{
    private readonly JwtOptions _options = jwtOptions.Value;

    public string GenerateAccessToken(User user)
    {
        var rsa = RSA.Create();
        rsa.ImportFromPem(_options.RsaPrivateKeyPem);
        var credentials = new SigningCredentials(new RsaSecurityKey(rsa), SecurityAlgorithms.RsaSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Name, user.Name)
        };

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_options.ExpiryMinutes),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GenerateRefreshToken()
    {
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
    }
}
