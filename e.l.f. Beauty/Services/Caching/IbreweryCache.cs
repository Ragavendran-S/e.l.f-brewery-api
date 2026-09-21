using e.l.f._Beauty.Models;

public interface IBreweryCache
{
    Task<IEnumerable<Brewery>> GetOrFetchAsync(string key, Func<Task<IEnumerable<Brewery>>> fetch);
    /// <summary>
    /// Invalidate any cache entries whose key starts with the provided prefix.
    /// Used by repository decorators to evict related list entries after writes.
    /// </summary>
    void InvalidateByPrefix(string prefix);

    /// <summary>
    /// Remove a specific cache entry by exact key.
    /// </summary>
    void Remove(string key);
    /// <summary>
    /// Register a cache key in the internal registry without fetching its value.
    /// Useful to track keys for later prefix invalidation.
    /// </summary>
    void RegisterKey(string key);
}