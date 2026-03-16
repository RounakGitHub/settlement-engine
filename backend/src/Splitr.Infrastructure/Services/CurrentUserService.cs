using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Splitr.Application.Interfaces;

namespace Splitr.Infrastructure.Services;

public class CurrentUserService(IHttpContextAccessor httpContextAccessor) : ICurrentUserService
{
    public Guid UserId
    {
        get
        {
            var sub = httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
            return sub is not null ? Guid.Parse(sub) : Guid.Empty;
        }
    }
}
