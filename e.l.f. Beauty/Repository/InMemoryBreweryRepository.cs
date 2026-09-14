using e.l.f._Beauty.Models;
using Microsoft.Extensions.Caching.Memory;

public class CachedBreweryRepository : IBreweryRepository
{
    private readonly IBreweryRepository _inner;
    private readonly IMemoryCache _cache;
    private readonly ILogger<CachedBreweryRepository> _logger;

    public CachedBreweryRepository(IBreweryRepository inner, IMemoryCache cache, ILogger<CachedBreweryRepository> logger)
    {
        _inner = inner;
        _cache = cache;
        _logger = logger;
    }

    public async Task<IEnumerable<Brewery>> GetBreweriesAsync()
    {
        return await _cache.GetOrCreateAsync("breweries", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10);
            _logger.LogInformation("Cache miss for breweries at {Time}", DateTime.UtcNow);
            return await _inner.GetBreweriesAsync();
        });
    }

    public async Task<IEnumerable<Brewery>> SearchBreweriesAsync(string query)
    {
        return await _cache.GetOrCreateAsync("Search", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10);
            _logger.LogInformation("Cache miss for breweries at {Time}", DateTime.UtcNow);
            return await _inner.SearchBreweriesAsync(query);
        });
    }
    public async Task<IEnumerable<Brewery?>> GetBreweryByNameAsync(string name)
    {
        return await _cache.GetOrCreateAsync($"brewery:{name}", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10);
            _logger.LogInformation("Cache miss for brewery {Id} at {Time}", name, DateTime.UtcNow);
            return await _inner.GetBreweryByNameAsync(name);
        });
    }

    public async Task AddBreweryAsync(Brewery brewery)
    {
        // Add directly to inner rep\
        await _inner.AddBreweryAsync(brewery);

        // Invalidate cache so next read is fresh
        _cache.Remove("breweries");
        _cache.Remove($"brewery:{brewery.Id}");
        _logger.LogInformation("Cache invalidated after adding brewery {Id} at {Time}", brewery.Id, DateTime.UtcNow);
    }
}
