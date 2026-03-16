using Splitr.Application.Interfaces;
using StackExchange.Redis;

namespace Splitr.Infrastructure.Caching;

public class ValkeyIdempotencyService(IConnectionMultiplexer connection) : IIdempotencyService
{
    private const string KeyPrefix = "idempotency:";

    public async Task SetAsync(string key, string response, TimeSpan ttl, CancellationToken ct)
    {
        var db = connection.GetDatabase();
        await db.StringSetAsync(KeyPrefix + key, response, ttl);
    }

    public async Task<string?> GetAsync(string key, CancellationToken ct)
    {
        var db = connection.GetDatabase();
        var value = await db.StringGetAsync(KeyPrefix + key);
        return value.HasValue ? value.ToString() : null;
    }
}
