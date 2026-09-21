using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using e.l.f._Beauty.Models;
using e.l.f._Beauty.Repository;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace e.l.f._Beauty.Tests.Repository
{
    public class BreweryRepositoryWrapperTests
    {
        private BreweryDbContext CreateInMemoryContext()
        {
            var options = new DbContextOptionsBuilder<BreweryDbContext>()
                .UseInMemoryDatabase(databaseName: "repo-wrapper-db-" + Guid.NewGuid())
                .Options;
            return new BreweryDbContext(options);
        }

        [Fact]
        public async Task GetBreweriesAsync_WithDbContext_UsesDbAndDoesNotCallUpstream()
        {
            using var ctx = CreateInMemoryContext();

            // seed 12 breweries
            var items = Enumerable.Range(1, 12)
                .Select(i => new Brewery { Id = i.ToString(), Name = $"Local {i:000}", City = i % 2 == 0 ? "Even" : "Odd" })
                .ToList();
            ctx.Breweries.AddRange(items);
            await ctx.SaveChangesAsync();

            var upstream = new FailingUpstream(); // will throw if called
            var logger = new NullLogger<BreweryRepository>();
            var paging = new e.l.f._Beauty.Services.PagingHelper();
            var repo = new BreweryRepository(upstream, ctx, logger, paging);

            var options = new BreweryQueryOptions { Page = 2, PageSize = 5, SortBy = "Name", Ascending = true };
            var res = (await repo.GetBreweriesAsync(options)).ToList();

            // Expect page 2 with 5 items => items 6..10 when ordered by Name
            Assert.Equal(5, res.Count);
            Assert.Equal("Local 006", res.First().Name);
            Assert.Equal("Local 010", res.Last().Name);
        }

        [Fact]
        public async Task GetBreweriesAsync_WithoutDbContext_DelegatesToUpstream()
        {
            var upstream = new TestUpstream();
            var logger = new NullLogger<BreweryRepository>();
            var paging = new e.l.f._Beauty.Services.PagingHelper();
            var repo = new BreweryRepository(upstream, null, logger, paging);

            var options = new BreweryQueryOptions { Page = 1, PageSize = 10 };
            var res = (await repo.GetBreweriesAsync(options)).ToList();

            Assert.Single(res);
            Assert.Equal("Upstream 1", res[0].Name);
            Assert.True(upstream.WasCalled);
        }

        // Upstream that will throw if used - ensures DB path is taken when context available
        private class FailingUpstream : IUpstreamBreweryClient
        {
            public Task<HttpResponseMessage> PostBreweryAsync(Brewery brewery) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
            public Task<IEnumerable<Brewery>> GetBreweryByNameAsync(string name) => throw new InvalidOperationException("Should not be called");
            public Task<IEnumerable<Brewery>> GetBreweriesAsync(BreweryQueryOptions? options) => throw new InvalidOperationException("Should not be called");
            public Task<IEnumerable<Brewery>> SearchBreweriesAsync(string query) => throw new InvalidOperationException("Should not be called");
        }

        // Simple upstream stub for delegation test
        private class TestUpstream : IUpstreamBreweryClient
        {
            public bool WasCalled { get; private set; }
            public Task<HttpResponseMessage> PostBreweryAsync(Brewery brewery) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
            public Task<IEnumerable<Brewery>> GetBreweryByNameAsync(string name) => Task.FromResult<IEnumerable<Brewery>>(new[] { new Brewery { Id = "u1", Name = "UpstreamMatch" } });
            public Task<IEnumerable<Brewery>> GetBreweriesAsync(BreweryQueryOptions? options)
            {
                WasCalled = true;
                var list = new List<Brewery> { new Brewery { Id = "u1", Name = "Upstream 1" } };
                return Task.FromResult<IEnumerable<Brewery>>(list);
            }
            public Task<IEnumerable<Brewery>> SearchBreweriesAsync(string query)
            {
                WasCalled = true;
                var list = new List<Brewery> { new Brewery { Id = "u1", Name = "Upstream 1" } };
                return Task.FromResult<IEnumerable<Brewery>>(list);
            }
        }
    }
}
