using System.Collections.Generic;
using AutoMapper;
using Xunit;
using e.l.f._Beauty.Models;

namespace e.l.f._Beauty.Tests.Services
{
    public class DistanceSorterTests
    {
        private readonly IMapper _mapper;
        private readonly DistanceSorter _sorter;

        public DistanceSorterTests()
        {
            var config = new MapperConfiguration(cfg => cfg.AddProfile<BreweryProfile>());
            config.AssertConfigurationIsValid();
            _mapper = config.CreateMapper();

            _sorter = new DistanceSorter();
        }

        [Fact]
        public void Mapping_Parses_LatitudeLongitude_And_Populates_Brewery()
        {
            var external = new ExternalBrewery
            {
                brewery_id = "1",
                brewery_name = "Test Brewery",
                location_city = "City",
                location_state = "State",
                location_country = "Country",
                latitude = "37.7749",
                longitude = "-122.4194"
            };

            var brewery = _mapper.Map<Brewery>(external);

            Assert.NotNull(brewery);
            Assert.NotNull(brewery.Latitude);
            Assert.NotNull(brewery.Longitude);
            Assert.Equal(37.7749, brewery.Latitude.Value, 4);
            Assert.Equal(-122.4194, brewery.Longitude.Value, 4);
        }

        [Fact]
        public void DistanceSorter_Sorts_By_Distance_Ascending()
        {
            // User location near Los Angeles
            var userLat = 34.0522;
            var userLng = -118.2437;

            // External breweries: San Francisco and Los Angeles
            var externalList = new List<ExternalBrewery>
            {
                new ExternalBrewery { brewery_id = "sf", brewery_name = "SF Brewery", latitude = "37.7749", longitude = "-122.4194" },
                new ExternalBrewery { brewery_id = "la", brewery_name = "LA Brewery", latitude = "34.0522", longitude = "-118.2437" }
            };

            var breweries = new List<Brewery>();
            foreach (var ext in externalList)
            {
                breweries.Add(_mapper.Map<Brewery>(ext));
            }

            var options = new BreweryQueryOptions { UserLat = userLat, UserLng = userLng, Ascending = true };

            var sorted = _sorter.Sort(breweries, options);
            var first = System.Linq.Enumerable.First(sorted);

            // LA should be closer to LA user location
            Assert.Equal("la", first.Id);
        }
    }
}
