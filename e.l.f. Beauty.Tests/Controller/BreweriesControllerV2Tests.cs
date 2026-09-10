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
    public class BreweriesControllerV2Tests
    {
        private readonly Mock<IBreweryService> _serviceMock;
        private readonly Mock<ILogger<BreweryService>> _loggerMock;
        private readonly BreweriesControllerV2 _controller;

        public BreweriesControllerV2Tests()
        {
            _serviceMock = new Mock<IBreweryService>();
            _loggerMock = new Mock<ILogger<BreweryService>>();
            _controller = new BreweriesControllerV2(_serviceMock.Object, _loggerMock.Object);
        }

        [Fact]
        public async Task GetBreweries_ReturnsOk_WithPagedResult_ItemsMatch()
        {
            // Arrange
            var sample = new List<Brewery>
            {
                new Brewery { Name = "Alpha Brewery", City = "CityA" },
                new Brewery { Name = "Beta Brewery", City = "CityB" }
            };

            // IMPORTANT: Use the actual constructor signature of your PagedResult<T>.
            // This example assumes: PagedResult<T>(IEnumerable<T> items, int total, int page, int pageSize)
            var paged = new PagedResult<Brewery>(sample, 2, 1, 10);

            _serviceMock
                .Setup(s => s.GetBreweriesAsync(It.IsAny<BreweryQueryOptions>()))
                .ReturnsAsync(paged);

            // Act
            var actionResult = await _controller.GetBreweries(new BreweryQueryOptions());

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(actionResult);
            Assert.NotNull(okResult.Value);

            // The returned value should be the same PagedResult<Brewery> instance (or equivalent)
            var returned = Assert.IsType<PagedResult<Brewery>>(okResult.Value);
            Assert.NotNull(returned.items);
            Assert.Equal(2, returned.items.Count());
            Assert.Contains(returned.items, b => b.Name == "Alpha Brewery");
            Assert.Contains(returned.items, b => b.Name == "Beta Brewery");
        }

        [Fact]
        public async Task GetBreweries_ServiceThrows_PropagatesException()
        {
            // Arrange
            _serviceMock
                .Setup(s => s.GetBreweriesAsync(It.IsAny<BreweryQueryOptions>()))
                .ThrowsAsync(new HttpRequestException("External API failure"));

            // Act & Assert
            await Assert.ThrowsAsync<HttpRequestException>(() => _controller.GetBreweries(new BreweryQueryOptions()));
        }
    }
}
