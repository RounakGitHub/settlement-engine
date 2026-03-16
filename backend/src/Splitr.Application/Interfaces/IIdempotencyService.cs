namespace Splitr.Application.Interfaces;

public interface IIdempotencyService
{
    Task SetAsync(string key, string response, TimeSpan ttl, CancellationToken ct);
    Task<string?> GetAsync(string key, CancellationToken ct);
}
