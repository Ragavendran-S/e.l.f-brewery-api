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
        var result = await _cache.GetOrCreateAsync(key, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10);
            _logger.LogInformation("Cache miss for {Key}", key);
            return await fetch();
        });

        return result ?? Enumerable.Empty<Brewery>();
    }
}