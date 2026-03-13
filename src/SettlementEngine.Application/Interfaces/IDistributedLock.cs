namespace SettlementEngine.Application.Interfaces;

public interface IDistributedLock : IAsyncDisposable
{
    bool IsAcquired { get; }
}

public interface IDistributedLockProvider
{
    Task<IDistributedLock> AcquireAsync(string resource, TimeSpan expiry, CancellationToken ct = default);
}
