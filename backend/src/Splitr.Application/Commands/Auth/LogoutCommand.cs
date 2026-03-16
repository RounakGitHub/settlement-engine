using Splitr.Application.Mediator;

namespace Splitr.Application.Commands.Auth;

public record LogoutCommand : IRequest<Unit>;
