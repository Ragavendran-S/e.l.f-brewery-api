using e.l.f._Beauty.Models;
using Microsoft.Extensions.Caching.Memory;

public class CachedBreweryRepository : IBreweryRepository
{
    private const string BreweriesCacheName = "breweries";
    private readonly IBreweryRepository _inner;
    private readonly IMemoryCache _cache;
    private readonly IBreweryCache _registryCache;
    private readonly ILogger<CachedBreweryRepository> _logger;

    public CachedBreweryRepository(IBreweryRepository inner, IMemoryCache cache, ILogger<CachedBreweryRepository> logger, IBreweryCache registryCache)
    {
        _inner = inner;
        _cache = cache;
        _logger = logger;
        _registryCache = registryCache;
    }

    public async Task<BulkInsertResult> AddBreweriesAsync(IEnumerable<Brewery> breweries)
    {
        if (breweries == null) throw new ArgumentNullException(nameof(breweries));
        // Delegate to inner repository which may support bulk operations
        var res = await _inner.AddBreweriesAsync(breweries);

        // Invalidate caches by prefix so variant list queries are cleared
            try
            {
                _registryCache.InvalidateByPrefix($"{BreweriesCacheName}:");
                foreach (var b in breweries)
                {
                    // Guard against null elements in the incoming collection to satisfy nullable analysis
                    if (b == null) continue;
                    _registryCache.Remove($"{BreweriesCacheName}:brewery:{b.Id}");
                }
            }
        catch
        {
            // Fallback to direct removal if registry not available or fails
            _cache.Remove(BreweriesCacheName);
                foreach (var b in breweries)
                {
                    if (b == null) continue;
                    _cache.Remove($"{BreweriesCacheName}:brewery:{b.Id}");
                }
        }
        _logger.LogInformation("Cache invalidated after adding breweries at {Time}", DateTime.UtcNow);
        return res;
    }

    public async Task<IEnumerable<Brewery>> GetBreweriesAsync(BreweryQueryOptions options)
    {
        var cacheKey = options == null ? $"{BreweriesCacheName}:all" : $"{BreweriesCacheName}:page={options.Page}:size={options.PageSize}:search={options.Search}:city={options.City}:sort={options.SortBy}";
        // Ensure key is recorded in registry via the IBreweryCache implementation
        _registryCache?.GetOrFetchAsync(cacheKey, () => _inner.GetBreweriesAsync(options ?? new BreweryQueryOptions()));

        // Provide total count when inner repository can supply it. If inner
        // does not support totals the registry/cache callers will receive null
        // and the service layer will handle bounded-prefix paging.
        // Try to call inner.GetTotalCountAsync if available via interface.
        try
        {
            var totalTask = _inner.GetTotalCountAsync(options ?? new BreweryQueryOptions());
            // store result in registry if needed (best-effort)
            totalTask.ContinueWith(t => { /* no-op here */ });
        }
        catch
        {
            // Ignore if inner does not implement the method (older implementations)
        }

        var result = await _cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10);
            _logger.LogInformation("Cache miss for breweries at {Time}", DateTime.UtcNow);
            // Ensure we never pass a null options object to the inner repository implementation
            return await _inner.GetBreweriesAsync(options ?? new BreweryQueryOptions());
        });

        return result ?? Enumerable.Empty<Brewery>();
    }

    public async Task<IEnumerable<Brewery>> SearchBreweriesAsync(string query)
    {
        var result = await _cache.GetOrCreateAsync($"{BreweriesCacheName}:search:{query}", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10);
            _logger.LogInformation("Cache miss for breweries at {Time}", DateTime.UtcNow);
            return await _inner.SearchBreweriesAsync(query);
        });

        return result ?? Enumerable.Empty<Brewery>();
    }
    public async Task<IEnumerable<Brewery?>> GetBreweryByNameAsync(string name)
    {
        var result = await _cache.GetOrCreateAsync($"{BreweriesCacheName}:brewery:{name}", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10);
            _logger.LogInformation("Cache miss for brewery {Id} at {Time}", name, DateTime.UtcNow);
            return await _inner.GetBreweryByNameAsync(name);
        });

        return result ?? Enumerable.Empty<Brewery?>();
    }

    public async Task AddBreweryAsync(Brewery brewery)
    {
        // Add directly to inner rep\
        await _inner.AddBreweryAsync(brewery);

        // Invalidate cache so next read is fresh
        _cache.Remove(BreweriesCacheName);
        _cache.Remove($"{BreweriesCacheName}:brewery:{brewery.Id}");
        _logger.LogInformation("Cache invalidated after adding brewery {Id} at {Time}", brewery.Id, DateTime.UtcNow);
    }

    public async Task<int?> GetTotalCountAsync(BreweryQueryOptions options)
    {
        try
        {
            return await _inner.GetTotalCountAsync(options);
        }
        catch
        {
            // If inner does not support totals or fails, return null to indicate
            // unknown total so callers (service) will apply bounded-prefix logic.
            return null;
        }
    }
}
