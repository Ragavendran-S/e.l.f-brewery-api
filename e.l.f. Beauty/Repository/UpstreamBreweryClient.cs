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
        private const string AlternateBaseUrl = "https://api.openbrewerydb.org/breweries";

        public UpstreamBreweryClient(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        // Helper that performs a GET and returns the response body as string.
        // If the upstream API returns a generic welcome message (shape containing
        // a top-level "message" property) attempt a retry using an alternate
        // base URL (some API deployments respond differently to the /v1 prefix).
        private async Task<string> GetStringWithFallbackAsync(string url)
        {
            var resp = await _httpClient.GetAsync(url);

            if (resp.IsSuccessStatusCode)
            {
                var json = await resp.Content.ReadAsStringAsync();

                if (LooksLikeWelcomeMessage(json))
                {
                    var altUrl = url;
                    if (url.StartsWith(BaseUrl, System.StringComparison.OrdinalIgnoreCase))
                    {
                        altUrl = AlternateBaseUrl + url.Substring(BaseUrl.Length);
                    }
                    else if (url.Contains("/v1/"))
                    {
                        altUrl = url.Replace("/v1/", "/");
                    }

                    if (!string.Equals(altUrl, url, System.StringComparison.Ordinal))
                    {
                        var altResp = await _httpClient.GetAsync(altUrl);
                        if (altResp.IsSuccessStatusCode)
                        {
                            return await altResp.Content.ReadAsStringAsync();
                        }

                        if (altResp.StatusCode == System.Net.HttpStatusCode.NotFound)
                        {
                            return "[]";
                        }

                        altResp.EnsureSuccessStatusCode();
                    }
                }

                return json;
            }

            if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                var altUrl = url;
                if (url.StartsWith(BaseUrl, System.StringComparison.OrdinalIgnoreCase))
                {
                    altUrl = AlternateBaseUrl + url.Substring(BaseUrl.Length);
                }
                else if (url.Contains("/v1/"))
                {
                    altUrl = url.Replace("/v1/", "/");
                }

                if (!string.Equals(altUrl, url, System.StringComparison.Ordinal))
                {
                    var altResp = await _httpClient.GetAsync(altUrl);
                    if (altResp.IsSuccessStatusCode)
                    {
                        return await altResp.Content.ReadAsStringAsync();
                    }

                    if (altResp.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        return "[]";
                    }

                    altResp.EnsureSuccessStatusCode();
                }

                return "[]";
            }

            resp.EnsureSuccessStatusCode();
            return "[]";
        }

        private static bool LooksLikeWelcomeMessage(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return false;

            try
            {
                using var doc = JsonDocument.Parse(json);
                return doc.RootElement.ValueKind == JsonValueKind.Object
                    && doc.RootElement.TryGetProperty("message", out _);
            }
            catch (JsonException)
            {
                return false;
            }
        }

        public async Task<IEnumerable<Brewery>> GetBreweryByNameAsync(string name)
        {
            // Try a set of candidate upstream endpoints until one returns usable results.
            var candidates = new List<string>();

            // Primary by_name endpoint (legacy)
            candidates.Add(Microsoft.AspNetCore.WebUtilities.QueryHelpers.AddQueryString(BaseUrl, "by_name", name ?? string.Empty));
            // Alternate without /v1
            candidates.Add(Microsoft.AspNetCore.WebUtilities.QueryHelpers.AddQueryString(AlternateBaseUrl, "by_name", name ?? string.Empty));
            // Search endpoint (more likely to be supported)
            candidates.Add(Microsoft.AspNetCore.WebUtilities.QueryHelpers.AddQueryString(BaseUrl + "/search", "query", name ?? string.Empty));
            candidates.Add(Microsoft.AspNetCore.WebUtilities.QueryHelpers.AddQueryString(AlternateBaseUrl + "/search", "query", name ?? string.Empty));
            // Fallback to autocomplete which may return partial shapes
            candidates.Add(Microsoft.AspNetCore.WebUtilities.QueryHelpers.AddQueryString(BaseUrl + "/autocomplete", "query", name ?? string.Empty));
            candidates.Add(Microsoft.AspNetCore.WebUtilities.QueryHelpers.AddQueryString(AlternateBaseUrl + "/autocomplete", "query", name ?? string.Empty));

            var result = await TryGetBreweriesFromUrlsAsync(candidates);
            return result;
        }

        public async Task<IEnumerable<Brewery>> GetBreweriesAsync(BreweryQueryOptions? options)
        {
            // The upstream API does not support our full query options. If a search term is provided prefer the
            // autocomplete/search endpoint. Otherwise fall back to fetching the first page worth of items.
            if (!string.IsNullOrWhiteSpace(options?.Search))
            {
                return await SearchBreweriesAsync(options.Search);
            }

            var json = await GetStringWithFallbackAsync(BaseUrl);
            return JsonSerializer.Deserialize<IEnumerable<Brewery>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? Enumerable.Empty<Brewery>();
        }

        private async Task<IEnumerable<Brewery>> TryGetBreweriesFromUrlsAsync(IEnumerable<string> urls)
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            foreach (var url in urls)
            {
                try
                {
                    var resp = await _httpClient.GetAsync(url);
                    if (!resp.IsSuccessStatusCode)
                    {
                        if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
                            continue; // try next candidate
                        resp.EnsureSuccessStatusCode();
                    }

                    var json = await resp.Content.ReadAsStringAsync();

                    // Skip generic welcome/message responses
                    if (!string.IsNullOrWhiteSpace(json) && json.IndexOf("\"message\"", StringComparison.OrdinalIgnoreCase) >= 0)
                        continue;

                    // Try to deserialize into full Brewery objects
                    try
                    {
                        var breweries = JsonSerializer.Deserialize<IEnumerable<Brewery>>(json, options);
                        if (breweries != null && breweries.Any())
                            return breweries;
                    }
                    catch (JsonException)
                    {
                        // ignored - try tolerant parsing below
                    }

                    // Tolerant parsing for autocomplete shapes
                    try
                    {
                        using var doc = JsonDocument.Parse(json);
                        if (doc.RootElement.ValueKind == JsonValueKind.Array)
                        {
                            var list = new List<Brewery>();
                            foreach (var item in doc.RootElement.EnumerateArray())
                            {
                                if (item.ValueKind == JsonValueKind.String)
                                {
                                    list.Add(new Brewery { Name = item.GetString() ?? string.Empty });
                                    continue;
                                }

                                if (item.ValueKind == JsonValueKind.Object)
                                {
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

                                    if (item.TryGetProperty("latitude", out var latProp))
                                    {
                                        if (latProp.ValueKind == JsonValueKind.Number && latProp.TryGetDouble(out var dbl))
                                            b.Latitude = dbl;
                                        else if (latProp.ValueKind == JsonValueKind.String && double.TryParse(latProp.GetString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var parsedLat))
                                            b.Latitude = parsedLat;
                                    }

                                    if (item.TryGetProperty("longitude", out var lngProp))
                                    {
                                        if (lngProp.ValueKind == JsonValueKind.Number && lngProp.TryGetDouble(out var dblLng))
                                            b.Longitude = dblLng;
                                        else if (lngProp.ValueKind == JsonValueKind.String && double.TryParse(lngProp.GetString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var parsedLng))
                                            b.Longitude = parsedLng;
                                    }

                                    list.Add(b);
                                }
                            }

                            if (list.Any())
                                return list;
                        }
                    }
                    catch (JsonException)
                    {
                        // if we can't parse, try next candidate
                    }
                }
                catch (HttpRequestException)
                {
                    // try next candidate
                }
            }

            return Enumerable.Empty<Brewery>();
        }

        // Retained explicitly to preserve search support and interface compatibility.
        public async Task<IEnumerable<Brewery>> SearchBreweriesAsync(string query)
        {
            var url = Microsoft.AspNetCore.WebUtilities.QueryHelpers.AddQueryString(BaseUrl + "/autocomplete", "query", query ?? string.Empty);
            var json = await GetStringWithFallbackAsync(url);

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

                        // Parse latitude/longitude tolerantly and assign to the Brewery object.
                        if (item.TryGetProperty("latitude", out var latProp))
                        {
                            if (latProp.ValueKind == JsonValueKind.Number && latProp.TryGetDouble(out var dbl))
                            {
                                b.Latitude = dbl;
                            }
                            else if (latProp.ValueKind == JsonValueKind.String)
                            {
                                if (double.TryParse(latProp.GetString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var parsedLat))
                                    b.Latitude = parsedLat;
                            }
                        }

                        if (item.TryGetProperty("longitude", out var lngProp))
                        {
                            if (lngProp.ValueKind == JsonValueKind.Number && lngProp.TryGetDouble(out var dblLng))
                            {
                                b.Longitude = dblLng;
                            }
                            else if (lngProp.ValueKind == JsonValueKind.String)
                            {
                                if (double.TryParse(lngProp.GetString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var parsedLng))
                                    b.Longitude = parsedLng;
                            }
                        }

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
