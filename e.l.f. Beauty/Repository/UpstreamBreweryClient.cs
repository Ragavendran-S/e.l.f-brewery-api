using System.Net.Http;
using System.Text.Json;
using e.l.f._Beauty.Models;

namespace e.l.f._Beauty.Repository
{
    public interface IUpstreamBreweryClient
    {
        Task<IEnumerable<Brewery>> GetBreweriesAsync(BreweryQueryOptions? options);
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

        public async Task<IEnumerable<Brewery>> GetBreweriesAsync(BreweryQueryOptions? options)
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

            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            try
            {
                // Try to deserialize into the expected full Brewery shape first
                var breweries = JsonSerializer.Deserialize<IEnumerable<Brewery>>(json, options);
                if (breweries != null)
                    return breweries;
            }
            catch (JsonException)
            {
                // Fall through and attempt tolerant parsing below
            }

            // Be tolerant of alternate shapes returned by the autocomplete endpoint.
            // It may return an array of objects with a subset of fields, or an array of
            // strings. Parse generically and map to Brewery where possible.
            try
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.ValueKind != JsonValueKind.Array)
                    return Enumerable.Empty<Brewery>();

                var list = new List<Brewery>();
                foreach (var item in doc.RootElement.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String)
                    {
                        var name = item.GetString() ?? string.Empty;
                        list.Add(new Brewery { Name = name });
                        continue;
                    }

                    if (item.ValueKind == JsonValueKind.Object)
                    {
                        // Attempt to read common properties
                        var b = new Brewery();
                        if (item.TryGetProperty("name", out var nameProp) && nameProp.ValueKind == JsonValueKind.String)
                            b.Name = nameProp.GetString() ?? string.Empty;
                        if (item.TryGetProperty("id", out var idProp) && idProp.ValueKind == JsonValueKind.String)
                            b.Id = idProp.GetString() ?? string.Empty;
                        if (item.TryGetProperty("street", out var streetProp) && streetProp.ValueKind == JsonValueKind.String)
                            b.Street = streetProp.GetString() ?? string.Empty;
                        if (item.TryGetProperty("city", out var cityProp) && cityProp.ValueKind == JsonValueKind.String)
                            b.City = cityProp.GetString() ?? string.Empty;
                        if (item.TryGetProperty("state", out var stateProp) && stateProp.ValueKind == JsonValueKind.String)
                            b.State = stateProp.GetString() ?? string.Empty;
                        if (item.TryGetProperty("country", out var countryProp) && countryProp.ValueKind == JsonValueKind.String)
                            b.Country = countryProp.GetString() ?? string.Empty;
                        if (item.TryGetProperty("phone", out var phoneProp) && phoneProp.ValueKind == JsonValueKind.String)
                            b.Phone = phoneProp.GetString() ?? string.Empty;
                        if (item.TryGetProperty("latitude", out var latProp) && latProp.ValueKind == JsonValueKind.String)
                            double.TryParse(latProp.GetString(), out var latVal);
                        if (item.TryGetProperty("longitude", out var lngProp) && lngProp.ValueKind == JsonValueKind.String)
                            double.TryParse(lngProp.GetString(), out var lngVal);

                        list.Add(b);
                    }
                }

                return list;
            }
            catch (JsonException)
            {
                // If parsing fails, return empty list rather than throw to avoid 500s
                return Enumerable.Empty<Brewery>();
            }
        }

        public async Task<HttpResponseMessage> PostBreweryAsync(Brewery brewery)
        {
            var json = JsonSerializer.Serialize(brewery);
            using var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            // Use the absolute BaseUrl so this call works even when the HttpClient has no BaseAddress configured.
            return await _httpClient.PostAsync(BaseUrl, content);
        }
    }
}
