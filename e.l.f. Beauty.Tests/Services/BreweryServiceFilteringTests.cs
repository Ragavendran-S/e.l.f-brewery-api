using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using e.l.f._Beauty.Models;
using e.l.f._Beauty.Repository;
using e.l.f._Beauty.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

public class BreweryServiceFilteringTests
{
    private readonly IMapper _mapper;

    public BreweryServiceFilteringTests()
    {
        var config = AutoMapperTestHelper.CreateConfiguration(cfg => cfg.AddMaps(typeof(BreweryProfile).Assembly));
        _mapper = config.CreateMapper();
    }

    [Fact]
    public async Task GetBreweriesAsync_DoesNotReapplyFilter_WhenRepositorySupportsServerSideFiltering()
    {
        var repo = new RepoSupportsFiltering();
        var cache = new TestCache();
        var logger = new NullLogger<BreweryService>();
        var service = new BreweryService(repo, cache, logger, _mapper, new BreweryFilter(), new BrewerySorterFactory(), new PagingHelper());

        // Use a search term that would normally filter out the returned items
        var options = new BreweryQueryOptions { Page = 1, PageSize = 10, Search = "zz" };
        var result = await service.GetBreweriesAsync(options);

        // Repo returned two items; because it advertises server-side filtering the service must not re-filter them
        Assert.Equal(2, result.Items.Count);
    }

    [Fact]
    public async Task GetBreweriesAsync_AppliesFilter_WhenRepositoryDoesNotSupportServerSideFiltering()
    {
        var repo = new RepoNoSupportFiltering();
        var cache = new TestCache();
        var logger = new NullLogger<BreweryService>();
        var service = new BreweryService(repo, cache, logger, _mapper, new BreweryFilter(), new BrewerySorterFactory(), new PagingHelper());

        var options = new BreweryQueryOptions { Page = 1, PageSize = 10, Search = "Alpha" };
        var result = await service.GetBreweriesAsync(options);

        // Repo returned two items but service should filter down to the one matching "Alpha"
        Assert.Single(result.Items);
        Assert.Equal("Alpha Brewery", result.Items.First().Name);
    }

    // Test doubles
    private class RepoSupportsFiltering : IBreweryRepository
    {
        public Task AddBreweryAsync(Brewery brewery) => Task.CompletedTask;
        public Task<BulkInsertResult> AddBreweriesAsync(IEnumerable<Brewery> breweries) => Task.FromResult(new BulkInsertResult { Total = breweries.Count(), SuccessCount = breweries.Count(), FailedCount = 0 });
        public Task<IEnumerable<Brewery>> GetBreweriesAsync(BreweryQueryOptions options)
        {
            // Return two items regardless of requested search so we can assert the service doesn't re-filter
            var list = new List<Brewery>
            {
                new Brewery { Id = "1", Name = "Alpha Brewery" },
                new Brewery { Id = "2", Name = "Beta Brewery" }
            };
            return Task.FromResult<IEnumerable<Brewery>>(list);
        }
        public Task<int?> GetTotalCountAsync(BreweryQueryOptions options) => Task.FromResult<int?>(2);
        public Task<IEnumerable<Brewery>> SearchBreweriesAsync(string query)
        {
            var list = new List<Brewery> { new Brewery { Id = "1", Name = "Lagunitas Brewing Co" } };
            return Task.FromResult<IEnumerable<Brewery>>(list);
        }
        public Task<IEnumerable<Brewery?>> GetBreweryByNameAsync(string name) => Task.FromResult<IEnumerable<Brewery?>>(new List<Brewery?>());
        public bool SupportsServerSideFiltering() => true;
    }

    private class RepoNoSupportFiltering : IBreweryRepository
    {
        public Task AddBreweryAsync(Brewery brewery) => Task.CompletedTask;
        public Task<BulkInsertResult> AddBreweriesAsync(IEnumerable<Brewery> breweries) => Task.FromResult(new BulkInsertResult { Total = breweries.Count(), SuccessCount = breweries.Count(), FailedCount = 0 });
        public Task<IEnumerable<Brewery>> GetBreweriesAsync(BreweryQueryOptions options)
        {
            // Return an unfiltered list where only one matches the search term
            var list = new List<Brewery>
            {
                new Brewery { Id = "1", Name = "Alpha Brewery" },
                new Brewery { Id = "2", Name = "Gamma Brewery" }
            };
            return Task.FromResult<IEnumerable<Brewery>>(list);
        }
        public Task<int?> GetTotalCountAsync(BreweryQueryOptions options) => Task.FromResult<int?>(null);
        public Task<IEnumerable<Brewery>> SearchBreweriesAsync(string query)
        {
            var list = new List<Brewery> { new Brewery { Id = "1", Name = "Lagunitas Brewing Co" } };
            return Task.FromResult<IEnumerable<Brewery>>(list);
        }
        public Task<IEnumerable<Brewery?>> GetBreweryByNameAsync(string name) => Task.FromResult<IEnumerable<Brewery?>>(new List<Brewery?>());
        // Leave SupportsServerSideFiltering default (false)
    }

    private class TestCache : IBreweryCache
    {
        private readonly Dictionary<string, IEnumerable<Brewery>> _store = new();
        public Task<IEnumerable<Brewery>> GetOrFetchAsync(string key, Func<Task<IEnumerable<Brewery>>> fetch)
        {
            if (_store.TryGetValue(key, out var v)) return Task.FromResult(v);
            var fetched = fetch().GetAwaiter().GetResult();
            _store[key] = fetched;
            return Task.FromResult(fetched);
        }

        public void InvalidateByPrefix(string prefix) { }
        public void Remove(string key) { _store.Remove(key); }
        public void RegisterKey(string key) { }
    }
}
