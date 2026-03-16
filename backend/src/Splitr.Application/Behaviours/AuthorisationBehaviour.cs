using Splitr.Application.Mediator;
using Microsoft.EntityFrameworkCore;
using Splitr.Application.Exceptions;
using Splitr.Application.Interfaces;

namespace Splitr.Application.Behaviours;

public interface IRequireGroupMembership
{
    Guid GroupId { get; }
}

public class AuthorisationBehaviour<TRequest, TResponse>(
    ICurrentUserService currentUserService,
    IAppDbContext dbContext
) : IPipelineBehavior<TRequest, TResponse> where TRequest : IRequireGroupMembership
{
    public async Task<TResponse> Handle(
        TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken
    )
    {
        var isMember = await dbContext.GroupMembers
            .AnyAsync(
                gm => gm.GroupId == request.GroupId && gm.UserId == currentUserService.UserId,
                cancellationToken
            );

        if (!isMember)
            throw new ForbiddenAccessException(
                $"User {currentUserService.UserId} is not a member of group {request.GroupId}.");

        return await next();
    }
}
