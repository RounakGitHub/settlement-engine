using Splitr.Application.Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Splitr.Application.Commands.Auth;
using Splitr.Application.Configuration;
using Splitr.Application.Interfaces;

namespace Splitr.Application.Handlers.Auth;

public class UpdateProfileCommandHandler(
    IAppDbContext dbContext,
    ICurrentUserService currentUser,
    IOptions<AuthOptions> authOptions) : IRequestHandler<UpdateProfileCommand, Unit>
{
    private readonly AuthOptions _auth = authOptions.Value;

    public async Task<Unit> Handle(UpdateProfileCommand request, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == currentUser.UserId, cancellationToken)
            ?? throw new KeyNotFoundException("User not found.");

        var trimmedName = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(trimmedName) || trimmedName.Length > 100)
            throw new ArgumentException("Name must be between 1 and 100 characters.");

        user.Name = trimmedName;

        if (!string.IsNullOrWhiteSpace(request.Password))
        {
            if (request.Password.Length < 8)
                throw new ArgumentException("Password must be at least 8 characters.");

            user.PasswordHash = BCrypt.Net.BCrypt.EnhancedHashPassword(request.Password, _auth.BcryptCost);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
