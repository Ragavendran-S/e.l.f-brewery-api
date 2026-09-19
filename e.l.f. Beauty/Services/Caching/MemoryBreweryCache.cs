using e.l.f._Beauty.Models;
using Microsoft.Extensions.Caching.Memory;

public class MemoryBreweryCache : IBreweryCache
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<MemoryBreweryCache> _logger;

    public MemoryBreweryCache(IMemoryCache cache, ILogger<MemoryBreweryCache> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public async Task<IEnumerable<Brewery>> GetOrFetchAsync(string key, Func<Task<IEnumerable<Brewery>>> fetch)
    {
        // Instrument cache lookups so runtime logs show which keys are used.
        if (_cache.TryGetValue(key, out IEnumerable<Brewery>? existing) && existing != null)
        {
            _logger.LogDebug("Cache hit for {Key}", key);
            return existing;
        }

        _logger.LogDebug("Cache lookup miss for {Key}, fetching from source", key);
        var result = await _cache.GetOrCreateAsync(key, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10);
            _logger.LogInformation("Cache miss creating entry for {Key}", key);
            return await fetch();
        });

        return result ?? Enumerable.Empty<Brewery>();
    }
}