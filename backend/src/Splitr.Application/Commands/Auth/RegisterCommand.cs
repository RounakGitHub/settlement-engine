using Splitr.Application.Mediator;

namespace Splitr.Application.Commands.Auth;

public record AuthResult(string AccessToken, string RefreshToken, Guid UserId, string UserName);

public record RegisterCommand(string Name, string Email, string Password) : IRequest<AuthResult>;
