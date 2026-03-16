using System.Security.Claims;
using System.Security.Cryptography;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Splitr.Infrastructure.Configuration;

namespace Splitr.API.Configuration;

public static class AuthConfiguration
{
    public static IServiceCollection AddAuth(this IServiceCollection services, IConfiguration configuration)
    {
        var jwtSection = configuration.GetSection(JwtOptions.SectionName);
        services.Configure<JwtOptions>(jwtSection);

        // RSA private key: load from environment variable (never commit to source control)
        var rsaPrivateKeyPem = Environment.GetEnvironmentVariable("JWT_RSA_PRIVATE_KEY_PEM")
            ?? jwtSection["RsaPrivateKeyPem"]
            ?? throw new InvalidOperationException(
                "JWT signing key not configured. Set the JWT_RSA_PRIVATE_KEY_PEM environment variable.");

        var rsa = RSA.Create();
        rsa.ImportFromPem(rsaPrivateKeyPem);

        var googleClientId = configuration["Google:ClientId"];
        var googleClientSecret = configuration["Google:ClientSecret"];
        var googleEnabled = !string.IsNullOrEmpty(googleClientId) && !string.IsNullOrEmpty(googleClientSecret);

        var authBuilder = services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwtSection["Issuer"],
                ValidAudience = jwtSection["Audience"],
                IssuerSigningKey = new RsaSecurityKey(rsa),
                ClockSkew = TimeSpan.Zero
            };

            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    // 1. SignalR sends token as query param (WebSocket can't send cookies in all transports)
                    var accessToken = context.Request.Query["access_token"];
                    var path = context.HttpContext.Request.Path;

                    if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/api/hubs"))
                    {
                        context.Token = accessToken;
                        return Task.CompletedTask;
                    }

                    // 2. For all other requests, read JWT from HttpOnly cookie
                    if (context.Request.Cookies.TryGetValue("accessToken", out var cookieToken))
                    {
                        context.Token = cookieToken;
                    }

                    return Task.CompletedTask;
                }
            };
        });

        // Only register Google OAuth when credentials are configured
        if (googleEnabled)
        {
            authBuilder
                .AddCookie("ExternalCookie", options =>
                {
                    options.Cookie.SameSite = SameSiteMode.Lax;
                    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                })
                .AddGoogle(options =>
                {
                    options.SignInScheme = "ExternalCookie";
                    options.ClientId = googleClientId!;
                    options.ClientSecret = googleClientSecret!;
                    options.CallbackPath = "/api/auth/google/callback";
                    // Always show the account picker so users can switch accounts.
                    options.Events.OnRedirectToAuthorizationEndpoint = context =>
                    {
                        context.Response.Redirect(context.RedirectUri + "&prompt=select_account");
                        return Task.CompletedTask;
                    };
                });
        }

        // Make google-enabled flag available to controllers
        services.AddSingleton(new GoogleAuthOptions { Enabled = googleEnabled });

        services.AddAuthorization();

        var rateLimits = configuration
            .GetSection(RateLimitOptions.SectionName)
            .Get<RateLimitOptions>()
            ?? throw new InvalidOperationException("RateLimiting configuration is missing.");

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            AddSlidingWindowPolicy(options, "auth-login", rateLimits.AuthLogin, partitionByIp: true);
            AddSlidingWindowPolicy(options, "auth-register", rateLimits.AuthRegister, partitionByIp: true);
            AddSlidingWindowPolicy(options, "write", rateLimits.Write, partitionByIp: false);
            AddSlidingWindowPolicy(options, "settlement", rateLimits.Settlement, partitionByIp: false);
            AddSlidingWindowPolicy(options, "general", rateLimits.General, partitionByIp: false);
        });

        return services;
    }

    private static void AddSlidingWindowPolicy(RateLimiterOptions options, string policyName, RateLimitPolicyOptions policy, bool partitionByIp)
    {
        options.AddPolicy(policyName, context =>
            RateLimitPartition.GetSlidingWindowLimiter(
                partitionByIp
                    ? context.Connection.RemoteIpAddress?.ToString() ?? "unknown"
                    : context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anonymous",
                _ => new SlidingWindowRateLimiterOptions
                {
                    PermitLimit = policy.PermitLimit,
                    Window = TimeSpan.FromSeconds(policy.WindowSeconds),
                    SegmentsPerWindow = policy.SegmentsPerWindow
                }
            )
        );
    }
}
