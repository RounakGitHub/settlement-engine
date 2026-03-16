using Splitr.Application.Mediator;

namespace Splitr.Application.Commands.Auth;

public record RefreshTokenCommand(string RefreshToken) : IRequest<AuthResult>;
