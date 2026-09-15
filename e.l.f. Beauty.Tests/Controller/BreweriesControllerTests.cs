using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using e.l.f._Beauty.Controllers;
using e.l.f._Beauty.Models;
using e.l.f._Beauty.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace e.l.f._Beauty.Tests.Controllers
{
    public class BreweriesControllerTests
    {
        private readonly Mock<IBreweryService> _serviceMock;
        private readonly Mock<ILogger<BreweryService>> _loggerMock;
        private readonly BreweriesController _controller;
        private readonly Mock<IBreweryRepository> _repository;

        public BreweriesControllerTests()
        {
            _serviceMock = new Mock<IBreweryService>();
            _loggerMock = new Mock<ILogger<BreweryService>>();
            _repository=new Mock<IBreweryRepository>();
            _controller = new BreweriesController(_serviceMock.Object, _loggerMock.Object,_repository.Object);
        }

        [Fact]
        
        public async Task GetBreweries_ReturnsOk_WithPagedResult()
        {
            // Arrange
            var sample = new List<Brewery>
            {
                new Brewery { Name = "Alpha Brewery", City = "CityA" },
                new Brewery { Name = "Beta Brewery", City = "CityB" }
            };
            var paged = new PagedResult<Brewery>(sample, 2, page: 1, pageSize: 10);

            _serviceMock
                .Setup(s => s.GetBreweriesAsync(It.IsAny<BreweryQueryOptions>()))
                .ReturnsAsync(paged);

            var options = new BreweryQueryOptions();

            // Act
            var actionResult = await _controller.GetBreweries(options);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
            var returned = Assert.IsType<PagedResult<Brewery>>(okResult.Value);

            // Assert on the items collection (always present)
            Assert.NotNull(returned.Items);
            Assert.Equal(2, returned.Items.Count());

            // If your PagedResult exposes a page or pageSize, you can assert those too:
            // Assert.Equal(1, returned.Page);
            // Assert.Equal(10, returned.PageSize);
        }


        [Fact]
        public async Task Autocomplete_WithValidQuery_ReturnsOk_WithList()
        {
            // Arrange
            var expected = new List<Brewery>
            {
                new Brewery { Name = "Lagunitas", City = "Petaluma" }
            };

            _serviceMock
                .Setup(s => s.AutocompleteAsync("lag"))
                .ReturnsAsync(expected);

            // Act
            var result = await _controller.Autocomplete("lag");

            // Assert
            var ok = Assert.IsType<OkObjectResult>(result);
            var returned = Assert.IsAssignableFrom<IEnumerable<Brewery>>(ok.Value);
            Assert.Single(returned);
        }

        [Fact]
        public async Task Autocomplete_WithEmptyQuery_ReturnsBadRequest()
        {
            // Arrange
            string query = "   "; // whitespace

            // Act
            var result = await _controller.Autocomplete(query);

            // Assert
            var bad = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal("Query cannot be empty.", bad.Value);
        }

        [Fact]
        public async Task Autocomplete_ServiceThrowsHttpRequestException_Returns503()
        {
            // Arrange
            _serviceMock
                .Setup(s => s.AutocompleteAsync("lag"))
                .ThrowsAsync(new HttpRequestException("External API down"));

            // Act
            var result = await _controller.Autocomplete("lag");

            // Assert
            var status = Assert.IsType<ObjectResult>(result);
            Assert.Equal(503, status.StatusCode);
            Assert.Equal("External API unavailable.", status.Value);

            // Verify logger was called at least once with a warning (optional)
            _loggerMock.Verify(
                x => x.Log(
                    It.Is<LogLevel>(l => l == LogLevel.Warning),
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.AtLeastOnce);
        }
    }
}
