using System.Collections.Generic;
using System.Linq;
using e.l.f._Beauty.Models;
using Xunit;

namespace e.l.f._Beauty.Tests.Services
{
    public class BreweryFilterTests
    {
        [Fact]
        public void Apply_FiltersBySearch()
        {
            var filter = new BreweryFilter();
            var breweries = new List<Brewery>
            {
                new Brewery { Id = "1", Name = "Lager House", City = "CityA" },
                new Brewery { Id = "2", Name = "Ale Works", City = "CityB" }
            };

            var options = new BreweryQueryOptions { Search = "lager" };
            var result = filter.Apply(breweries, options).ToList();

            Assert.Single(result);
            Assert.Equal("1", result[0].Id);
        }

        [Fact]
        public void Apply_FiltersByCity()
        {
            var filter = new BreweryFilter();
            var breweries = new List<Brewery>
            {
                new Brewery { Id = "1", Name = "Lager House", City = "CityA" },
                new Brewery { Id = "2", Name = "Ale Works", City = "CityB" }
            };

            var options = new BreweryQueryOptions { City = "CityB" };
            var result = filter.Apply(breweries, options).ToList();

            Assert.Single(result);
            Assert.Equal("2", result[0].Id);
        }
    }
}
