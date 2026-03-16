using Splitr.Application.Mediator;

namespace Splitr.Application.Commands.Settlements;

public record ProcessWebhookCommand(string RawBody, string Signature, string SourceIp) : IRequest<Unit>;
