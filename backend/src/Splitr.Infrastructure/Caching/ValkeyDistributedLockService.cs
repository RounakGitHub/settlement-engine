using Splitr.Application.Interfaces;
using StackExchange.Redis;

namespace Splitr.Infrastructure.Caching;

public class ValkeyDistributedLockService(IConnectionMultiplexer connection) : IDistributedLockService
{
    private const string KeyPrefix = "lock:";
    private static readonly RedisValue LockValue = Environment.MachineName;

    public async Task<bool> AcquireAsync(string key, TimeSpan ttl, CancellationToken ct)
    {
        var db = connection.GetDatabase();
        return await db.LockTakeAsync(KeyPrefix + key, LockValue, ttl);
    }

    public async Task ReleaseAsync(string key, CancellationToken ct)
    {
        var db = connection.GetDatabase();
        await db.LockReleaseAsync(KeyPrefix + key, LockValue);
    }
}
