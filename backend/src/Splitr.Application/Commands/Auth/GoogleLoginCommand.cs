using Splitr.Application.Mediator;

namespace Splitr.Application.Commands.Auth;

public record GoogleLoginCommand(string GoogleId, string Email, string Name) : IRequest<AuthResult>;
