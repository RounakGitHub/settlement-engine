namespace Splitr.Application.Interfaces;

public interface IDistributedLockService
{
    Task<bool> AcquireAsync(string key, TimeSpan ttl, CancellationToken ct);
    Task ReleaseAsync(string key, CancellationToken ct);
}
