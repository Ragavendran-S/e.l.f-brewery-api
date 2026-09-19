using System.Net.Http;
using System.Text.Json;
using e.l.f._Beauty.Models;

namespace e.l.f._Beauty.Repository
{
    public interface IUpstreamBreweryClient
    {
        Task<IEnumerable<Brewery>> GetBreweriesAsync(BreweryQueryOptions options);
        Task<IEnumerable<Brewery>> GetBreweryByNameAsync(string name);
        Task<IEnumerable<Brewery>> SearchBreweriesAsync(string query);
        Task<HttpResponseMessage> PostBreweryAsync(Brewery brewery);
    }

    public class UpstreamBreweryClient : IUpstreamBreweryClient
    {
        private readonly HttpClient _httpClient;
        private const string BaseUrl = "https://api.openbrewerydb.org/v1/breweries";

        public UpstreamBreweryClient(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<IEnumerable<Brewery>> GetBreweryByNameAsync(string name)
        {
            // Build query with proper encoding to avoid injection or malformed URLs
            var url = Microsoft.AspNetCore.WebUtilities.QueryHelpers.AddQueryString(BaseUrl, "by_name", name ?? string.Empty);
            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<IEnumerable<Brewery>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? Enumerable.Empty<Brewery>();
        }

        public async Task<IEnumerable<Brewery>> GetBreweriesAsync(BreweryQueryOptions options)
        {
            // The upstream API does not support our full query options. If a search term is provided prefer the
            // autocomplete/search endpoint. Otherwise fall back to fetching the first page worth of items.
            if (!string.IsNullOrWhiteSpace(options?.Search))
            {
                return await SearchBreweriesAsync(options.Search);
            }

            var response = await _httpClient.GetAsync(BaseUrl);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<IEnumerable<Brewery>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? Enumerable.Empty<Brewery>();
        }

        // Retained explicitly to preserve search support and interface compatibility.
        public async Task<IEnumerable<Brewery>> SearchBreweriesAsync(string query)
        {
            var url = Microsoft.AspNetCore.WebUtilities.QueryHelpers.AddQueryString(BaseUrl + "/autocomplete", "query", query ?? string.Empty);
            var response = await _httpClient.GetAsync(url);
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
