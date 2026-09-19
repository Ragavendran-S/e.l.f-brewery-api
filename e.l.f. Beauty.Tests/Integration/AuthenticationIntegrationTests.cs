using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace e.l.f._Beauty.Tests.Integration
{
    public class AuthenticationIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly WebApplicationFactory<Program> _factory;

        public AuthenticationIntegrationTests(WebApplicationFactory<Program> factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task Login_Then_Access_Protected_Endpoint_Returns200()
        {
            var client = _factory.CreateClient();

            var loginPayload = new { username = "admin", password = "password" };
            var content = new StringContent(JsonSerializer.Serialize(loginPayload), Encoding.UTF8, "application/json");

            var loginResponse = await client.PostAsync("/api/auth/login", content);
            loginResponse.EnsureSuccessStatusCode();

            var json = await loginResponse.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var token = doc.RootElement.GetProperty("token").GetString();

            Assert.False(string.IsNullOrEmpty(token));

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var protectedResponse = await client.GetAsync("/api/test/protected");
            Assert.True(protectedResponse.IsSuccessStatusCode, await protectedResponse.Content.ReadAsStringAsync());
        }
    }
}
