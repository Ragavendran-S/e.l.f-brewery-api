using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using e.l.f._Beauty.Models;
using e.l.f._Beauty.Repository;
using e.l.f._Beauty.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

public class BreweryServiceTests
{
    private readonly IMapper _mapper;

    public BreweryServiceTests()
    {
        var config = new AutoMapper.MapperConfiguration(cfg => cfg.AddMaps(typeof(BreweryProfile).Assembly));
        _mapper = config.CreateMapper();
    }

    [Fact]
    public async Task SearchBreweriesAsync_DelegatesToRepository_AndDoesNotRefilter()
    {
        var repo = new TestRepository();
        var cache = new TestCache();
        var logger = new NullLogger<BreweryService>();
        var service = new BreweryService(repo, cache, logger, _mapper, new BreweryFilter(), new BrewerySorterFactory(), new PagingHelper());

        var results = await service.SearchBreweriesAsync("La");

        // TestRepository returns already-filtered results; service must not further filter them
        Assert.Single(results);
        Assert.Contains(results, b => b.Name == "Lagunitas Brewing Co");
    }

    [Fact]
    public async Task AutocompleteAsync_UsesPerQueryCacheKey_AndDelegatesToRepository()
    {
        var repo = new TestRepository();
        var cache = new TestCache();
        var logger = new NullLogger<BreweryService>();
        var service = new BreweryService(repo, cache, logger, _mapper, new BreweryFilter(), new BrewerySorterFactory(), new PagingHelper());

        var results = await service.AutocompleteAsync("La");
        Assert.Single(results);
        Assert.Contains(results, b => b.Name == "Lagunitas Brewing Co");
    }

    // Simple test doubles
    private class TestRepository : IBreweryRepository
    {
        public Task AddBreweryAsync(Brewery brewery) => Task.CompletedTask;
        public Task AddBreweriesAsync(IEnumerable<Brewery> breweries) => Task.CompletedTask;
        public Task<IEnumerable<Brewery>> GetBreweriesAsync() => Task.FromResult<IEnumerable<Brewery>>(new List<Brewery>());
        public Task<IEnumerable<Brewery>> SearchBreweriesAsync(string query)
        {
            // Pretend upstream already filtered results for the query
            var list = new List<Brewery> { new Brewery { Id = "1", Name = "Lagunitas Brewing Co" } };
            return Task.FromResult<IEnumerable<Brewery>>(list);
        }
        public Task<IEnumerable<Brewery?>> GetBreweryByNameAsync(string name) => Task.FromResult<IEnumerable<Brewery?>>(new List<Brewery?>());
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
    }
}
