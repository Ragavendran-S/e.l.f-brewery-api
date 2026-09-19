using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using e.l.f._Beauty.Models;
using e.l.f._Beauty.Repository;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace e.l.f._Beauty.Tests.Repository
{
    public class EfCoreBreweryRepositoryPagingTests
    {
        private BreweryDbContext CreateInMemoryContext()
        {
            var options = new DbContextOptionsBuilder<BreweryDbContext>()
                .UseInMemoryDatabase(databaseName: "test-db-paging-" + Guid.NewGuid())
                .Options;
            return new BreweryDbContext(options);
        }

        [Fact]
        public async Task GetBreweriesAsync_AppliesPaging_WhenOptionsProvided()
        {
            using var ctx = CreateInMemoryContext();
            // Seed 20 breweries with predictable names
            var items = Enumerable.Range(1, 20)
                .Select(i => new Brewery { Id = i.ToString(), Name = $"Brewery {i:000}" })
                .ToList();
            ctx.Breweries.AddRange(items);
            await ctx.SaveChangesAsync();

            var repo = new ElfBreweryApi.Repositories.EfCoreBreweryRepository(ctx);

            var options = new BreweryQueryOptions { Page = 2, PageSize = 5, SortBy = "Name", Ascending = true };
            var page = await repo.GetBreweriesAsync(options);

            var list = page.ToList();
            Assert.Equal(5, list.Count);
            // Expect items 6..10 based on name ordering
            Assert.Equal("Brewery 006", list.First().Name);
            Assert.Equal("Brewery 010", list.Last().Name);
        }
    }
}
