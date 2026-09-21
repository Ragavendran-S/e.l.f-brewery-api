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
            // Treat an empty cached collection or a cached placeholder with empty items as a cache miss
            if (!existing.Any() || existing.All(b => string.IsNullOrEmpty(b?.Id) && string.IsNullOrWhiteSpace(b?.Name)))
            {
                _logger.LogDebug("Cache contains empty/placeholder value for {Key}; treating as miss and refetching", key);
            }
            else
            {
                _logger.LogDebug("Cache hit for {Key}", key);
                return existing;
            }
        }

        _logger.LogDebug("Cache lookup miss for {Key}, fetching from source", key);

        // Record the cache key in a registry so we can support prefix invalidation later.
        const string KEY_REGISTRY = "__brewery_cache_keys__";
        var registry = _cache.GetOrCreate(KEY_REGISTRY, entry => new HashSet<string>());
        registry.Add(key);
        _cache.Set(KEY_REGISTRY, registry, TimeSpan.FromMinutes(60));

        var result = await _cache.GetOrCreateAsync(key, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10);
            _logger.LogInformation("Cache miss creating entry for {Key}", key);
            return await fetch();
        });

        return result ?? Enumerable.Empty<Brewery>();
    }

    public void InvalidateByPrefix(string prefix)
    {
        // IMemoryCache does not provide prefix invalidation out of the box. To support
        // this in a simple way, store known keys in a set under a reserved key and use
        // that list to enumerate and remove matching keys.
        const string KEY_REGISTRY = "__brewery_cache_keys__";
        if (!_cache.TryGetValue<HashSet<string>>(KEY_REGISTRY, out var registry) || registry == null)
            return;

        var toRemove = registry.Where(k => k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).ToList();
        foreach (var k in toRemove)
        {
            _cache.Remove(k);
            registry.Remove(k);
            _logger.LogInformation("Invalidated cache entry {Key} by prefix {Prefix}", k, prefix);
        }

        // Update registry
        _cache.Set(KEY_REGISTRY, registry, TimeSpan.FromMinutes(60));
    }

    public void Remove(string key)
    {
        _cache.Remove(key);
    }
}