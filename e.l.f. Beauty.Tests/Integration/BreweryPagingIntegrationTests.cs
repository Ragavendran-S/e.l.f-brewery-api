using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using e.l.f._Beauty.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Xunit;
using e.l.f._Beauty.Repository;

namespace e.l.f._Beauty.Tests.Integration
{
    [Collection("IntegrationTests")]
    public class BreweryPagingIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly WebApplicationFactory<Program> _factory;

        public BreweryPagingIntegrationTests(WebApplicationFactory<Program> factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task GetBreweriesAsync_ReturnsPage2_WhenDbHasMultiplePages()
        {
            // Arrange: create factory with in-memory DB and seed > pageSize items
            var dbName = "paging-int-db-" + System.Guid.NewGuid().ToString();

            // Use a file-based SQLite DB for this test to verify DB-first seeding
            var dbFile = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"brewery-seed-{System.Guid.NewGuid()}.db");
            var clientFactory = _factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    // Replace authentication with a test handler that automatically
                    // authenticates requests so we can call protected endpoints.
                    services.AddAuthentication(options =>
                    {
                        options.DefaultAuthenticateScheme = "Test";
                        options.DefaultChallengeScheme = "Test";
                    }).AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, TestAuthHandler>("Test", opts => { });

                    // Replace DbContext registration with a file-based SQLite DB for this test
                    var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<BreweryDbContext>));
                    if (descriptor != null) services.Remove(descriptor);

                    services.AddDbContext<BreweryDbContext>(options =>
                        options.UseSqlite($"Data Source={dbFile}"));
                });
            });

            // Ensure DB file does not persist between test runs
            if (System.IO.File.Exists(dbFile)) System.IO.File.Delete(dbFile);

            // Act: create an HTTP client to spin up the test server; the startup
            // will apply migrations and create the SQLite file. Then seed using
            // the test server's service provider so the request pipeline observes
            // the same DB instance.
            using (var client = clientFactory.CreateClient())
            {
                using (var scope = clientFactory.Services.CreateScope())
                {
                    var ctx = scope.ServiceProvider.GetRequiredService<BreweryDbContext>();
                    // Apply migrations / ensure created
                    ctx.Database.EnsureCreated();
                    // Seed 12 breweries so pageSize=5 => page2 has items
                    for (int i = 1; i <= 12; i++)
                    {
                        ctx.Breweries.Add(new Brewery { Id = i.ToString(), Name = $"Local {i:000}" });
                    }
                    ctx.SaveChanges();
                }

                // Resolve the service from the test server's service provider and call it directly
                using (var scope = clientFactory.Services.CreateScope())
                {
                    var svc = scope.ServiceProvider.GetRequiredService<e.l.f._Beauty.Services.IBreweryService>();
                    var options = new BreweryQueryOptions { Page = 2, PageSize = 5, SortBy = "Name", Ascending = true };
                    var result = await svc.GetBreweriesAsync(options);

                    // Assert: Ensure items correspond to page 2 (items 6..10)
                    Assert.Equal(5, result.Items.Count);
                    Assert.Equal("Local 006", result.Items.First().Name);
                    Assert.Equal("Local 010", result.Items.Last().Name);
                }
            }

            // Act: resolve the service from the test server's service provider and call it directly
            using (var scope = clientFactory.Services.CreateScope())
            {
                var svc = scope.ServiceProvider.GetRequiredService<e.l.f._Beauty.Services.IBreweryService>();
                var options = new BreweryQueryOptions { Page = 2, PageSize = 5, SortBy = "Name", Ascending = true };
                var result = await svc.GetBreweriesAsync(options);

                // Assert: Ensure items correspond to page 2 (items 6..10)
                Assert.Equal(5, result.Items.Count);
                Assert.Equal("Local 006", result.Items.First().Name);
                Assert.Equal("Local 010", result.Items.Last().Name);
            }
        }
    }
}
