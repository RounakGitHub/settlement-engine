using Splitr.Application.Mediator;

namespace Splitr.Application.Queries;

public record GetProfileQuery : IRequest<ProfileResult>;

public record ProfileResult(string Name, string Email);
