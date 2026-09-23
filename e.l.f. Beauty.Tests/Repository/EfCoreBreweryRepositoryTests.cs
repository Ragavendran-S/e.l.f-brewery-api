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
    public class EfCoreBreweryRepositoryTests
    {
        private BreweryDbContext CreateInMemoryContext()
        {
            var options = new DbContextOptionsBuilder<BreweryDbContext>()
                .UseInMemoryDatabase(databaseName: "test-db-" + Guid.NewGuid())
                .Options;
            return new BreweryDbContext(options);
        }

        [Fact]
        public async Task AddBreweriesAsync_AddsRange_And_AllArePersisted()
        {
            using var ctx = CreateInMemoryContext();
            var repo = new e.l.f._Beauty.Repository.EfCoreBreweryRepository(ctx);

            var items = new List<Brewery>
            {
                new Brewery { Id = "a1", Name = "Alpha" },
                new Brewery { Id = "b2", Name = "Beta" },
                new Brewery { Id = "c3", Name = "Gamma" }
            };

            var res = await repo.AddBreweriesAsync(items);

            var persisted = await ctx.Breweries.AsNoTracking().ToListAsync();
            Assert.Equal(3, persisted.Count);
            Assert.Contains(persisted, b => b.Name == "Alpha");
            Assert.Contains(persisted, b => b.Name == "Beta");
            Assert.Contains(persisted, b => b.Name == "Gamma");
            Assert.Equal(3, res.SuccessCount);
            Assert.Equal(0, res.FailedCount);
        }
    }
}
