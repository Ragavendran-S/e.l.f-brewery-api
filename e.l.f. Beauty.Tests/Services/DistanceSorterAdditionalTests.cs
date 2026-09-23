using System.Collections.Generic;
using AutoMapper;
using Xunit;
using e.l.f._Beauty.Models;

namespace e.l.f._Beauty.Tests.Services
{
    public class DistanceSorterAdditionalTests
    {
        private readonly IMapper _mapper;
        private readonly DistanceSorter _sorter;

        public DistanceSorterAdditionalTests()
        {
            var config = AutoMapperTestHelper.CreateConfiguration(cfg => cfg.AddProfile<BreweryProfile>());
            config.AssertConfigurationIsValid();
            _mapper = config.CreateMapper();

            _sorter = new DistanceSorter();
        }

        [Fact]
        public void DistanceSorter_Sorts_By_Distance_Descending()
        {
            var userLat = 34.0522; // LA
            var userLng = -118.2437;

            var externalList = new List<ExternalBrewery>
            {
                new ExternalBrewery { brewery_id = "la", brewery_name = "LA Brewery", latitude = "34.0522", longitude = "-118.2437" },
                new ExternalBrewery { brewery_id = "sf", brewery_name = "SF Brewery", latitude = "37.7749", longitude = "-122.4194" }
            };

            var breweries = new List<Brewery>();
            foreach (var ext in externalList)
            {
                breweries.Add(_mapper.Map<Brewery>(ext));
            }

            var options = new BreweryQueryOptions { UserLat = userLat, UserLng = userLng, Ascending = false };

            var sorted = _sorter.Sort(breweries, options);
            var first = System.Linq.Enumerable.First(sorted);

            // Farthest first: SF should be before LA
            Assert.Equal("sf", first.Id);
        }

        [Fact]
        public void DistanceSorter_Missing_Coordinates_Are_Last()
        {
            var userLat = 34.0522;
            var userLng = -118.2437;

            var breweries = new List<Brewery>
            {
                new Brewery { Id = "a", Name = "A", Latitude = 34.0522, Longitude = -118.2437 },
                new Brewery { Id = "b", Name = "B", Latitude = null, Longitude = null },
                new Brewery { Id = "c", Name = "C", Latitude = 37.7749, Longitude = -122.4194 }
            };

            var options = new BreweryQueryOptions { UserLat = userLat, UserLng = userLng, Ascending = true };

            var sorted = _sorter.Sort(breweries, options);
            var last = System.Linq.Enumerable.Last(sorted);

            Assert.Equal("b", last.Id);
        }

        [Fact]
        public void DistanceSorter_Stable_When_Distances_Equal()
        {
            var userLat = 34.0;
            var userLng = -118.0;

            // Two breweries at identical coordinates
            var breweries = new List<Brewery>
            {
                new Brewery { Id = "first", Name = "First", Latitude = 35.0, Longitude = -119.0 },
                new Brewery { Id = "second", Name = "Second", Latitude = 35.0, Longitude = -119.0 }
            };

            var options = new BreweryQueryOptions { UserLat = userLat, UserLng = userLng, Ascending = true };

            var sorted = _sorter.Sort(breweries, options);
            var list = System.Linq.Enumerable.ToList(sorted);

            Assert.Equal("first", list[0].Id);
            Assert.Equal("second", list[1].Id);
        }
    }
}
