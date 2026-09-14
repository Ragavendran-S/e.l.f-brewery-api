using Microsoft.Extensions.Caching.Memory;

public static class MemoryCacheExtensions
{
    public static async Task<T> GetOrFetchAsync<T>(
        this IMemoryCache cache,
        string key,
        Func<Task<T>> fetch,
        TimeSpan? absoluteExpiration = null)
    {
        if (cache.TryGetValue(key, out T value))
        {
            return value;
        }

        value = await fetch();
        cache.Set(key, value, absoluteExpiration ?? TimeSpan.FromMinutes(10));
        return value;
    }
}
