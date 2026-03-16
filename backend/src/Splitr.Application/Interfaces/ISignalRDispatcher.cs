namespace Splitr.Application.Interfaces;

public interface ISignalRDispatcher
{
    Task DispatchAsync(Guid groupId, string eventType, string payload, CancellationToken ct);
}
