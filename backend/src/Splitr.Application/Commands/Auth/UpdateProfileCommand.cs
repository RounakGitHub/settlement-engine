using Splitr.Application.Mediator;

namespace Splitr.Application.Commands.Auth;

public record UpdateProfileCommand(string Name, string? Password = null) : IRequest<Unit>;
