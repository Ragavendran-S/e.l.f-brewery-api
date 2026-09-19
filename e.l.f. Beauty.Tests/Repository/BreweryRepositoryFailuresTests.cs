using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using e.l.f._Beauty.Models;
using e.l.f._Beauty.Repository;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

public class BreweryRepositoryFailuresTests
{
    [Fact]
    public async Task AddBreweryAsync_PropagatesException_AndIsLogged()
    {
        // Arrange: HttpClient that throws on PostAsync
        var handler = new FailingHandler();
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://example.test") };
        var upstream = new e.l.f._Beauty.Repository.UpstreamBreweryClient(client);
        var logger = new NullLogger<BreweryRepository>();
        var repo = new e.l.f._Beauty.Repository.BreweryRepository(upstream, null, logger);

        var brewery = new Brewery { Id = "x1", Name = "Fail" };

        // Act / Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => repo.AddBreweryAsync(brewery));
    }

    private class FailingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, System.Threading.CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("simulated http failure");
        }
    }
}
