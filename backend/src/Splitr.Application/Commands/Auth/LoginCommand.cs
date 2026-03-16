using Splitr.Application.Mediator;

namespace Splitr.Application.Commands.Auth;

public record LoginCommand(string Email, string Password) : IRequest<AuthResult>;
