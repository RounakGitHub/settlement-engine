using Splitr.Application.Mediator;
using Microsoft.EntityFrameworkCore;
using Splitr.Application.Interfaces;
using Splitr.Application.Queries;

namespace Splitr.Application.Handlers.Auth;

public class GetProfileQueryHandler(IAppDbContext dbContext, ICurrentUserService currentUser) : IRequestHandler<GetProfileQuery, ProfileResult>
{
    public async Task<ProfileResult> Handle(GetProfileQuery request, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == currentUser.UserId, cancellationToken)
            ?? throw new KeyNotFoundException("User not found.");

        return new ProfileResult(user.Name, user.Email);
    }
}
