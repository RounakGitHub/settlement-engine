using System.Security.Claims;
using Splitr.Application.Mediator;
using Splitr.API.Configuration;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Splitr.Application.Commands.Auth;
using Splitr.Application.Configuration;
using Splitr.Application.Queries;

namespace Splitr.API.Controllers;

[Route("api/auth")]
[ApiController]
public class AuthController(ISender sender, IOptions<AuthOptions> authOptions, IWebHostEnvironment env, GoogleAuthOptions googleAuth) : ControllerBase
{
    private readonly AuthOptions _auth = authOptions.Value;

    [HttpPost("register")]
    [AllowAnonymous]
    [EnableRateLimiting("auth-register")]
    public async Task<IActionResult> Register([FromBody] RegisterCommand command, CancellationToken ct)
    {
        var result = await sender.Send(command, ct);
        SetAuthCookies(result.AccessToken, result.RefreshToken);
        return Ok(new { result.UserId, result.UserName });
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("auth-login")]
    public async Task<IActionResult> Login([FromBody] LoginCommand command, CancellationToken ct)
    {
        var result = await sender.Send(command, ct);
        SetAuthCookies(result.AccessToken, result.RefreshToken);
        return Ok(new { result.UserId, result.UserName });
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh(CancellationToken ct)
    {
        var refreshToken = Request.Cookies[_auth.CookieName]
            ?? throw new UnauthorizedAccessException("No refresh token provided.");

        var result = await sender.Send(new RefreshTokenCommand(refreshToken), ct);
        SetAuthCookies(result.AccessToken, result.RefreshToken);
        return Ok(new { result.UserId, result.UserName });
    }

    [HttpGet("profile")]
    [Authorize]
    public async Task<IActionResult> GetProfile(CancellationToken ct)
    {
        var result = await sender.Send(new GetProfileQuery(), ct);
        return Ok(result);
    }

    [HttpPut("profile")]
    [Authorize]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileCommand command, CancellationToken ct)
    {
        await sender.Send(command, ct);
        return NoContent();
    }

    [HttpDelete("account")]
    [Authorize]
    public async Task<IActionResult> DeleteAccount(CancellationToken ct)
    {
        await sender.Send(new DeleteAccountCommand(), ct);
        Response.Cookies.Delete(_auth.CookieName, new CookieOptions { Path = _auth.CookiePath });
        Response.Cookies.Delete("accessToken", new CookieOptions { Path = "/" });
        return NoContent();
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        await sender.Send(new LogoutCommand(), ct);
        Response.Cookies.Delete(_auth.CookieName, new CookieOptions { Path = _auth.CookiePath });
        Response.Cookies.Delete("accessToken", new CookieOptions { Path = "/" });
        return NoContent();
    }

    [HttpGet("google")]
    [AllowAnonymous]
    public IActionResult GoogleLogin([FromQuery] string? returnUrl)
    {
        if (!googleAuth.Enabled)
            return BadRequest(new { title = "Google sign-in is not configured." });

        var properties = new AuthenticationProperties
        {
            RedirectUri = "/api/auth/google/signin"
        };
        return Challenge(properties, GoogleDefaults.AuthenticationScheme);
    }

    [HttpGet("google/signin")]
    [AllowAnonymous]
    public async Task<IActionResult> GoogleSignIn(CancellationToken ct)
    {
        if (!googleAuth.Enabled)
            return Redirect(GetFrontendUrl("/login?error=google_not_configured"));

        var result = await HttpContext.AuthenticateAsync("ExternalCookie");
        if (!result.Succeeded || result.Principal is null)
            return Redirect(GetFrontendUrl("/login?error=google_failed"));

        var googleId = result.Principal.FindFirstValue(ClaimTypes.NameIdentifier);
        var email = result.Principal.FindFirstValue(ClaimTypes.Email);
        var name = result.Principal.FindFirstValue(ClaimTypes.Name);

        if (string.IsNullOrEmpty(googleId) || string.IsNullOrEmpty(email))
            return Redirect(GetFrontendUrl("/login?error=google_missing_claims"));

        // Clean up the external cookie
        await HttpContext.SignOutAsync("ExternalCookie");

        var authResult = await sender.Send(
            new GoogleLoginCommand(googleId, email, name ?? email.Split('@')[0]),
            ct);

        SetAuthCookies(authResult.AccessToken, authResult.RefreshToken);

        // Redirect to frontend with userId/userName as query params so the SPA can hydrate auth store
        var frontendUrl = GetFrontendUrl(
            $"/auth/callback?userId={authResult.UserId}&userName={Uri.EscapeDataString(authResult.UserName)}");

        return Redirect(frontendUrl);
    }

    private void SetAuthCookies(string accessToken, string refreshToken)
    {
        var isDev = env.IsDevelopment();

        // Access token: Secure HttpOnly cookie, available to all API paths
        Response.Cookies.Append("accessToken", accessToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = !isDev,
            SameSite = isDev ? SameSiteMode.Lax : SameSiteMode.Strict,
            Path = "/",
            Expires = DateTimeOffset.UtcNow.AddMinutes(20) // slightly longer than JWT expiry for clock skew
        });

        // Refresh token: Secure HttpOnly cookie, scoped to auth path only
        Response.Cookies.Append(_auth.CookieName, refreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = !isDev,
            SameSite = isDev ? SameSiteMode.Lax : SameSiteMode.Strict,
            Path = _auth.CookiePath,
            Expires = DateTimeOffset.UtcNow.AddDays(_auth.RefreshTokenExpiryDays)
        });
    }

    private string GetFrontendUrl(string path)
    {
        var origin = HttpContext.Request.Headers.Origin.FirstOrDefault()
            ?? "http://localhost:3000";
        return $"{origin}{path}";
    }
}
