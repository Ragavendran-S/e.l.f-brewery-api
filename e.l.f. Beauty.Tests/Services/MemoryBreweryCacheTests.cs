using System.Collections.Generic;
using System.Threading.Tasks;
using e.l.f._Beauty.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace e.l.f._Beauty.Tests.Services
{
    public class MemoryBreweryCacheTests
    {
        [Fact]
        public async Task GetOrFetchAsync_CallsFetchOnce_WhenCached()
        {
            var memory = new MemoryCache(new MemoryCacheOptions());
            var logger = new NullLogger<MemoryBreweryCache>();
            var cache = new MemoryBreweryCache(memory, logger);

            int calls = 0;
            Task<IEnumerable<Brewery>> Fetch() => Task.FromResult<IEnumerable<Brewery>>(
                new List<Brewery> { new Brewery { Id = "1", Name = "Cached" } }
            );

            Task<IEnumerable<Brewery>> FetchCounting()
            {
                calls++;
                return Fetch();
            }

            // First call should invoke fetch
            var first = await cache.GetOrFetchAsync("key1", FetchCounting);
            Assert.Single(first);
            Assert.Equal(1, calls);

            // Second call should use cached value and not call fetch again
            var second = await cache.GetOrFetchAsync("key1", FetchCounting);
            Assert.Single(second);
            Assert.Equal(1, calls);
        }

        [Fact]
        public async Task GetOrFetchAsync_ReturnsEmpty_WhenFetchReturnsNull()
        {
            var memory = new MemoryCache(new MemoryCacheOptions());
            var logger = new NullLogger<MemoryBreweryCache>();
            var cache = new MemoryBreweryCache(memory, logger);

            int calls = 0;
            async Task<IEnumerable<Brewery>> FetchNull()
            {
                calls++;
                await Task.Yield();
                return null!; // simulate upstream returning null
            }

            var result = await cache.GetOrFetchAsync("nullKey", FetchNull);
            Assert.Empty(result);
            Assert.Equal(1, calls);

            // Subsequent calls should get cached empty result (fetch shouldn't be invoked again)
            var again = await cache.GetOrFetchAsync("nullKey", FetchNull);
            Assert.Empty(again);
            Assert.Equal(1, calls);
        }
    }
}
