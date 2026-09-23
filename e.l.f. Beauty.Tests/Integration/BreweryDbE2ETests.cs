using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Xunit;
using e.l.f._Beauty.Repository;
using e.l.f._Beauty.Models;

namespace e.l.f._Beauty.Tests.Integration
{
    [Collection("IntegrationTests")]
    public class BreweryDbE2ETests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly WebApplicationFactory<Program> _factory;

        public BreweryDbE2ETests(WebApplicationFactory<Program> factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task GetBreweries_ReturnsPage2_FromRealDb_Backend()
        {
            var dbFile = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"brewery-e2e-{System.Guid.NewGuid()}.db");

            using var clientFactory = _factory.WithWebHostBuilder(builder =>
            {
                using var sha512 = System.Security.Cryptography.SHA512.Create();
                var keyBytes = sha512.ComputeHash(System.Text.Encoding.UTF8.GetBytes("e2e-integration-key"));
                var base64Key = System.Convert.ToBase64String(keyBytes);
                System.Environment.SetEnvironmentVariable("Jwt__Key", base64Key);
                System.Environment.SetEnvironmentVariable("Jwt__Issuer", "brewery-api");
                System.Environment.SetEnvironmentVariable("Jwt__Audience", "brewery-api");

                builder.ConfigureServices(services =>
                {
                    // Replace DbContext registration with a file-based SQLite DB for this test
                    var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<BreweryDbContext>));
                    if (descriptor != null) services.Remove(descriptor);

                    services.AddDbContext<BreweryDbContext>(options =>
                        options.UseSqlite($"Data Source={dbFile}"));

                    // Replace authentication with TestAuthHandler to simplify calls
                    services.AddAuthentication(options =>
                    {
                        options.DefaultAuthenticateScheme = "Test";
                        options.DefaultChallengeScheme = "Test";
                    }).AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, TestAuthHandler>("Test", opts => { });
                });
            });

            // Ensure DB file does not persist between test runs
            if (System.IO.File.Exists(dbFile)) System.IO.File.Delete(dbFile);

            using (var client = clientFactory.CreateClient())
            {
                using (var scope = clientFactory.Services.CreateScope())
                {
                    var ctx = scope.ServiceProvider.GetRequiredService<BreweryDbContext>();
                    ctx.Database.EnsureCreated();

                    // Seed 12 breweries so pageSize=5 => page2 has items
                    for (int i = 1; i <= 12; i++)
                    {
                        ctx.Breweries.Add(new Brewery { Id = i.ToString(), Name = $"E2E Local {i:000}" });
                    }
                    ctx.SaveChanges();
                }

                // Call the public API endpoint to exercise the whole pipeline
                var response = await client.GetAsync("/api/v1/breweries?page=2&pageSize=5&sortBy=Name&asc=true");
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                // Expect a PagedResult shape with items array
                var items = doc.RootElement.GetProperty("items");
                Assert.Equal(5, items.GetArrayLength());
                Assert.Equal("E2E Local 006", items[0].GetProperty("name").GetString());
                Assert.Equal("E2E Local 010", items[4].GetProperty("name").GetString());
                }

                // Dispose the factory so the ASP.NET host and any open DB connections are closed
                // before attempting to remove the on-disk SQLite file. Call Dispose explicitly
                // to ensure the host shuts down synchronously in CI and local runs.
                clientFactory.Dispose();

                // Attempt to delete the file, retrying briefly if the OS still reports it as locked.
                // This is best-effort cleanup for test runs and should not fail the test if cleanup
                // is unsuccessful (the OS/temp directory will be cleaned up later).
                var attempts = 5;
                for (int i = 0; i < attempts; i++)
                {
                    try
                    {
                        if (System.IO.File.Exists(dbFile)) System.IO.File.Delete(dbFile);
                        break;
                    }
                    catch (System.IO.IOException)
                    {
                        // Wait briefly and retry
                        await Task.Delay(150);
                    }
                }
        }
    }
}
