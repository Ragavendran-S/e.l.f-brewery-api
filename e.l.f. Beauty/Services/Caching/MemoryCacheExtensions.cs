using Microsoft.Extensions.Caching.Memory;

public static class MemoryCacheExtensions
{
    public static async Task<T> GetOrFetchAsync<T>(
        this IMemoryCache cache,
        string key,
        Func<Task<T>> fetch,
        TimeSpan? absoluteExpiration = null)
    {
        if (cache.TryGetValue(key, out T? value) && value is not null)
        {
            return value;
        }

        var fetched = await fetch();
        if (fetched is null)
        {
            throw new InvalidOperationException($"Cache fetch for '{key}' returned null.");
        }

        cache.Set(key, fetched, absoluteExpiration ?? TimeSpan.FromMinutes(10));
        return fetched;
    }
}
