using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace e.l.f._Beauty.Tests.Integration
{
    [Collection("IntegrationTests")]
    public class InvalidQueryIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly WebApplicationFactory<Program> _factory;

        public InvalidQueryIntegrationTests(WebApplicationFactory<Program> factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task GetBreweries_SortByDistanceWithoutCoords_Returns400()
        {
            var clientFactory = _factory.WithWebHostBuilder(builder =>
            {
                // Provide deterministic JWT signing key for test runs so the host can start
                // Set environment variables here so Program.ResolveJwtKey and configuration
                // pick them up without requiring extra configuration assemblies in the test project.
                using var sha512 = System.Security.Cryptography.SHA512.Create();
                var keyBytes = sha512.ComputeHash(System.Text.Encoding.UTF8.GetBytes("e2e-integration-key"));
                var base64Key = System.Convert.ToBase64String(keyBytes);
                // Use the double-underscore form so ASP.NET configuration maps them to Jwt:Key etc.
                System.Environment.SetEnvironmentVariable("Jwt__Key", base64Key);
                System.Environment.SetEnvironmentVariable("Jwt__Issuer", "brewery-api");
                System.Environment.SetEnvironmentVariable("Jwt__Audience", "brewery-api");

                builder.ConfigureServices(services =>
                {
                    // Use test authentication so the request is treated as authenticated
                    services.AddAuthentication(options =>
                    {
                        options.DefaultAuthenticateScheme = "Test";
                        options.DefaultChallengeScheme = "Test";
                    }).AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, TestAuthHandler>(
                        "Test", opts => { });
                });
            });

            using var client = clientFactory.CreateClient();

            var resp = await client.GetAsync("/api/v1/breweries?sortBy=distance&page=1&pageSize=5");

            Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
            var content = await resp.Content.ReadAsStringAsync();
            Assert.Contains("User latitude and longitude are required", content);
        }
    }
}
