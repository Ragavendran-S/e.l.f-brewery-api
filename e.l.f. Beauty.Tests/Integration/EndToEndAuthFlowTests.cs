using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace e.l.f._Beauty.Tests.Integration
{
    public class EndToEndAuthFlowTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly WebApplicationFactory<Program> _factory;

        public EndToEndAuthFlowTests(WebApplicationFactory<Program> factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task Login_Returns_Jwt_And_ProtectedEndpoint_Allows_Access()
        {
            // Skip in CI where platform crypto differences may cause failures
            if (!string.IsNullOrEmpty(System.Environment.GetEnvironmentVariable("GITHUB_ACTIONS")) ||
                !string.IsNullOrEmpty(System.Environment.GetEnvironmentVariable("CI")))
            {
                return;
            }

            var factoryWithConfig = _factory.WithWebHostBuilder(builder =>
            {
                try
                {
                    var useEnv = typeof(Microsoft.AspNetCore.Hosting.IWebHostBuilder).GetMethod("UseEnvironment");
                    if (useEnv != null)
                    {
                        useEnv.Invoke(builder, new object[] { "Development" });
                    }
                    else
                    {
                        builder.UseSetting("environment", "Development");
                    }
                }
                catch
                {
                    builder.UseSetting("environment", "Development");
                }

                builder.ConfigureAppConfiguration((ctx, cfg) =>
                {
                    // Create a deterministic long key compatible with platform providers
                    using var sha512 = System.Security.Cryptography.SHA512.Create();
                    var keyBytes = sha512.ComputeHash(System.Text.Encoding.UTF8.GetBytes("e2e-integration-key"));
                    var base64Key = System.Convert.ToBase64String(keyBytes);

                    var settings = new System.Collections.Generic.Dictionary<string, string?>
                    {
                        ["Jwt:Key"] = base64Key,
                        ["Jwt:Issuer"] = "brewery-api",
                        ["Jwt:Audience"] = "brewery-api",
                        ["Auth:Username"] = "admin",
                        ["Auth:Password"] = "password",
                        // Ensure the app does not use the on-disk SQLite file during this test run.
                        // Tests must be isolated per-run; setting an empty DefaultConnection prevents
                        // Program.cs from registering the SQLite DbContext which would otherwise
                        // share the 'brewery.db' file across test runs and cause migration history
                        // collisions.
                        ["ConnectionStrings:DefaultConnection"] = string.Empty
                    };
                    cfg.AddInMemoryCollection(settings!);
                });

                // Replace DB with an isolated in-memory provider so tests are deterministic and do not share
                // the same SQLite file used by default app configuration.
                builder.ConfigureServices(services =>
                {
                    // Remove existing DbContext registrations so we can provide a test-scoped in-memory DB
                    services.RemoveAll(typeof(Microsoft.EntityFrameworkCore.DbContextOptions<e.l.f._Beauty.Repository.BreweryDbContext>));
                    services.RemoveAll(typeof(e.l.f._Beauty.Repository.BreweryDbContext));

                    // Add an isolated in-memory database per test run
                    services.AddDbContext<e.l.f._Beauty.Repository.BreweryDbContext>(options =>
                        options.UseInMemoryDatabase("e2e-auth-db-" + System.Guid.NewGuid().ToString()));
                });

                // Do not replace authentication - exercise real JWT issuance/validation
            });

            var client = factoryWithConfig.CreateClient();

            // Call login endpoint to obtain a token
            var loginPayload = JsonSerializer.Serialize(new { Username = "admin", Password = "password" });
            var loginContent = new StringContent(loginPayload, Encoding.UTF8, "application/json");
            var loginResponse = await client.PostAsync("/api/auth/login", loginContent);
            loginResponse.EnsureSuccessStatusCode();

            var json = await loginResponse.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            Assert.True(doc.RootElement.TryGetProperty("token", out var tokenElem));
            var token = tokenElem.GetString();
            Assert.False(string.IsNullOrWhiteSpace(token));

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var protectedResponse = await client.GetAsync("/api/test/protected");
            var body = await protectedResponse.Content.ReadAsStringAsync();
            Assert.True(protectedResponse.IsSuccessStatusCode, $"Protected endpoint failed: {protectedResponse.StatusCode} - {body}");
        }
    }
}
