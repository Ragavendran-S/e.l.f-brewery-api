using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using e.l.f._Beauty.Models;
using e.l.f._Beauty.Repository;
using e.l.f._Beauty.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

public class BreweryServiceAdditionalTests
{
    private readonly IMapper _mapper;

    public BreweryServiceAdditionalTests()
    {
        var config = new AutoMapper.MapperConfiguration(cfg => cfg.AddMaps(typeof(BreweryProfile).Assembly));
        _mapper = config.CreateMapper();
    }

    [Fact]
    public async Task GetBreweriesAsync_SortsByDistance_Ascending_ReturnsClosestFirst()
    {
        var repo = new TestRepositoryForSort();
        var cache = new TestCache();
        var logger = new NullLogger<BreweryService>();
        var service = new BreweryService(repo, cache, logger, _mapper, new BreweryFilter(), new BrewerySorterFactory(), new PagingHelper());

        var options = new BreweryQueryOptions { SortBy = "distance", UserLat = 37.0, UserLng = -122.0, Ascending = true, Page = 1, PageSize = 10 };
        var result = await service.GetBreweriesAsync(options);

        Assert.Equal(3, result.Items.Count);
        // Expect brewery A (closest), B, C (farthest)
        Assert.Equal("A", result.Items.ElementAt(0).Name);
        Assert.Equal("B", result.Items.ElementAt(1).Name);
        Assert.Equal("C", result.Items.ElementAt(2).Name);
    }

    [Fact]
    public async Task GetBreweriesAsync_Paging_ReturnsCorrectPage()
    {
        var repo = new TestRepositoryForPaging();
        var cache = new TestCache();
        var logger = new NullLogger<BreweryService>();
        var service = new BreweryService(repo, cache, logger, _mapper, new BreweryFilter(), new BrewerySorterFactory(), new PagingHelper());

        var options = new BreweryQueryOptions { Page = 2, PageSize = 2, SortBy = "name", Ascending = true };
        var result = await service.GetBreweriesAsync(options);

        Assert.Equal(2, result.Items.Count);
        Assert.Equal("C", result.Items.ElementAt(0).Name);
        Assert.Equal("D", result.Items.ElementAt(1).Name);
        Assert.Equal(4, result.TotalItems);
    }

    [Fact]
    public async Task GetBreweriesAsync_UsesCache_FetchOnce()
    {
        var repo = new TestRepositoryForPaging();
        var cache = new CountingTestCache();
        var logger = new NullLogger<BreweryService>();
        var service = new BreweryService(repo, cache, logger, _mapper, new BreweryFilter(), new BrewerySorterFactory(), new PagingHelper());

        var options = new BreweryQueryOptions { Page = 1, PageSize = 10, SortBy = "name" };
        var r1 = await service.GetBreweriesAsync(options);
        var r2 = await service.GetBreweriesAsync(options);

        Assert.Equal(1, ((CountingTestCache)cache).FetchCount);
        Assert.Equal(r1.TotalItems, r2.TotalItems);
    }

    // Test doubles
    private class TestRepositoryForSort : IBreweryRepository
    {
        public Task AddBreweryAsync(Brewery brewery) => Task.CompletedTask;
        public Task<BulkInsertResult> AddBreweriesAsync(IEnumerable<Brewery> breweries) => Task.FromResult(new BulkInsertResult { Total = breweries.Count(), SuccessCount = breweries.Count(), FailedCount = 0 });
        public Task<IEnumerable<Brewery>> GetBreweriesAsync(BreweryQueryOptions options) => Task.FromResult<IEnumerable<Brewery>>(new List<Brewery>
        {
            new Brewery { Id = "1", Name = "A", Latitude = 37.001, Longitude = -122.001 }, // closest
            new Brewery { Id = "2", Name = "B", Latitude = 37.01, Longitude = -122.01 },
            new Brewery { Id = "3", Name = "C", Latitude = 38.0, Longitude = -123.0 }
        });
        public Task<IEnumerable<Brewery>> SearchBreweriesAsync(string query) => Task.FromResult<IEnumerable<Brewery>>(new List<Brewery>());
        public Task<IEnumerable<Brewery?>> GetBreweryByNameAsync(string name) => Task.FromResult<IEnumerable<Brewery?>>(new List<Brewery?>());
    }

    private class TestRepositoryForPaging : IBreweryRepository
    {
        public Task AddBreweryAsync(Brewery brewery) => Task.CompletedTask;
        public Task<BulkInsertResult> AddBreweriesAsync(IEnumerable<Brewery> breweries) => Task.FromResult(new BulkInsertResult { Total = breweries.Count(), SuccessCount = breweries.Count(), FailedCount = 0 });
        public Task<IEnumerable<Brewery>> GetBreweriesAsync(BreweryQueryOptions options) => Task.FromResult<IEnumerable<Brewery>>(new List<Brewery>
        {
            new Brewery { Id = "1", Name = "A" },
            new Brewery { Id = "2", Name = "B" },
            new Brewery { Id = "3", Name = "C" },
            new Brewery { Id = "4", Name = "D" }
        });
        public Task<IEnumerable<Brewery>> SearchBreweriesAsync(string query) => Task.FromResult<IEnumerable<Brewery>>(new List<Brewery>());
        public Task<IEnumerable<Brewery?>> GetBreweryByNameAsync(string name) => Task.FromResult<IEnumerable<Brewery?>>(new List<Brewery?>());
    }

    private class TestCache : IBreweryCache
    {
        private readonly Dictionary<string, IEnumerable<Brewery>> _store = new();
        public virtual Task<IEnumerable<Brewery>> GetOrFetchAsync(string key, Func<Task<IEnumerable<Brewery>>> fetch)
        {
            if (_store.TryGetValue(key, out var v)) return Task.FromResult(v);
            var fetched = fetch().GetAwaiter().GetResult();
            _store[key] = fetched;
            return Task.FromResult(fetched);
        }

        public void InvalidateByPrefix(string prefix) { /* no-op for tests */ }
        public void Remove(string key) { _store.Remove(key); }
        public void RegisterKey(string key) { /* no-op for tests */ }
    }

    private class CountingTestCache : IBreweryCache
    {
        private readonly Dictionary<string, IEnumerable<Brewery>> _store = new();
        public int FetchCount { get; private set; }

        public Task<IEnumerable<Brewery>> GetOrFetchAsync(string key, Func<Task<IEnumerable<Brewery>>> fetch)
        {
            if (_store.TryGetValue(key, out var v))
            {
                return Task.FromResult(v);
            }

            FetchCount++;
            var fetched = fetch().GetAwaiter().GetResult();
            _store[key] = fetched;
            return Task.FromResult(fetched);
        }

        public void InvalidateByPrefix(string prefix) { /* no-op for tests */ }
        public void Remove(string key) { _store.Remove(key); }
        public void RegisterKey(string key) { /* no-op for tests */ }
    }
}
