using System.Net.Http;
using System.Text.Json;
using e.l.f._Beauty.Models;

namespace e.l.f._Beauty.Repository
{
    public interface IUpstreamBreweryClient
    {
        Task<IEnumerable<Brewery>> GetBreweriesAsync();
        Task<IEnumerable<Brewery>> GetBreweryByNameAsync(string name);
        Task<IEnumerable<Brewery>> SearchBreweriesAsync(string query);
        Task<HttpResponseMessage> PostBreweryAsync(Brewery brewery);
    }

    public class UpstreamBreweryClient : IUpstreamBreweryClient
    {
        private readonly HttpClient _httpClient;

        public UpstreamBreweryClient(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<IEnumerable<Brewery>> GetBreweryByNameAsync(string name)
        {
            var response = await _httpClient.GetAsync($"https://api.openbrewerydb.org/v1/breweries?by_name={Uri.EscapeDataString(name)}");
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<IEnumerable<Brewery>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? Enumerable.Empty<Brewery>();
        }

        public async Task<IEnumerable<Brewery>> GetBreweriesAsync()
        {
            var response = await _httpClient.GetAsync("https://api.openbrewerydb.org/v1/breweries");
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<IEnumerable<Brewery>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? Enumerable.Empty<Brewery>();
        }

        public async Task<IEnumerable<Brewery>> SearchBreweriesAsync(string query)
        {
            var response = await _httpClient.GetAsync($"https://api.openbrewerydb.org/v1/breweries/autocomplete?query={Uri.EscapeDataString(query)}");
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<IEnumerable<Brewery>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? Enumerable.Empty<Brewery>();
        }

        public async Task<HttpResponseMessage> PostBreweryAsync(Brewery brewery)
        {
            var json = JsonSerializer.Serialize(brewery);
            using var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            return await _httpClient.PostAsync("/v1/breweries", content);
        }
    }
}
