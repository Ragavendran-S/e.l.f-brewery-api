using System;
using System.Collections.Generic;
using AutoMapper;
using e.l.f._Beauty.Models;
using Xunit;

namespace e.l.f._Beauty.Tests.AutoMapper
{
    public class ExternalBreweryMappingTests
    {
        private readonly IMapper _mapper;

        public ExternalBreweryMappingTests()
        {
            var config = AutoMapperTestHelper.CreateConfiguration(cfg => cfg.AddProfile(new BreweryProfile()));
            config.AssertConfigurationIsValid();
            _mapper = config.CreateMapper();
        }

        [Fact]
        public void Map_MissingLatitudeLongitude_ResultsInNulls()
        {
            var external = new ExternalBrewery
            {
                brewery_id = "1",
                brewery_name = "NoCoords Brewery",
                location_city = "Nowhere",
                // latitude/longitude are intentionally left null
                latitude = null,
                longitude = null
            };

            var mapped = _mapper.Map<Brewery>(external);

            Assert.Equal("1", mapped.Id);
            Assert.Equal("NoCoords Brewery", mapped.Name);
            Assert.Null(mapped.Latitude);
            Assert.Null(mapped.Longitude);
        }

        [Fact]
        public void Map_MalformedLatitudeLongitude_ResultsInNulls()
        {
            var external = new ExternalBrewery
            {
                brewery_id = "2",
                brewery_name = "BadCoords Brewery",
                latitude = "N/A",
                longitude = "unknown"
            };

            var mapped = _mapper.Map<Brewery>(external);

            Assert.Equal("2", mapped.Id);
            Assert.Equal("BadCoords Brewery", mapped.Name);
            Assert.Null(mapped.Latitude);
            Assert.Null(mapped.Longitude);
        }

        [Fact]
        public void Map_ValidLatitudeLongitude_ParsesCorrectly()
        {
            var external = new ExternalBrewery
            {
                brewery_id = "3",
                brewery_name = "GoodCoords Brewery",
                latitude = "34.05",
                longitude = "-118.25"
            };

            var mapped = _mapper.Map<Brewery>(external);

            Assert.Equal("3", mapped.Id);
            Assert.Equal("GoodCoords Brewery", mapped.Name);
            Assert.Equal(34.05, mapped.Latitude);
            Assert.Equal(-118.25, mapped.Longitude);
        }
    }
}
