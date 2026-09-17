using System;
using e.l.f._Beauty.Models;
using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using System.Reflection;
using e.l.f.Validation;

namespace e.l.f._Beauty.Repository
{
    public class BreweryRepository : IBreweryRepository
    {
        private readonly HttpClient _httpClient;
        private readonly BreweryDbContext? _dbContext;

        public BreweryRepository(HttpClient httpClient, BreweryDbContext? dbContext = null)
        {
            _httpClient = httpClient;
            _dbContext = dbContext;
        }

        public async Task AddBreweryAsync(Brewery brewery)
        {
            if (brewery == null) throw new ArgumentNullException(nameof(brewery));

            // If a DbContext is available, persist to the local database.
            if (_dbContext != null)
            {
                _dbContext.Breweries.Add(brewery);
                await _dbContext.SaveChangesAsync();
                return;
            }

            // Otherwise, try to POST to the upstream API if supported (best-effort).
            // Many public brewery APIs are read-only; implement a safe no-op fallback.
            try
            {
                var json = JsonSerializer.Serialize(brewery);
                using var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync("/v1/breweries", content);
                // Treat non-success as a no-op but surface for diagnostics
                if (!response.IsSuccessStatusCode)
                {
                    // Do not throw here to avoid breaking callers when upstream is read-only.
                    return;
                }
            }
            catch
            {
                // Swallow exceptions for best-effort behavior; callers relying on persistence
                // should use an EFCore-backed repository.
            }
        }

        public async Task<IEnumerable<Brewery>> GetBreweriesAsync()
        {
            var response = await _httpClient.GetAsync("https://api.openbrewerydb.org/v1/breweries");
            response.EnsureSuccessStatusCode();
            if (!response.IsSuccessStatusCode)
            {
                // Map 4xx/5xx to UpstreamApiException with status and body
                throw new UpstreamApiException((int)response.StatusCode, $"Upstream returned {(int)response.StatusCode}", response.ReasonPhrase);
            }
            var json = await response.Content.ReadAsStringAsync();
            
            return JsonSerializer.Deserialize<IEnumerable<Brewery>>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? Enumerable.Empty<Brewery>();
        }

        public async Task<IEnumerable<Brewery?>> GetBreweryByNameAsync(string name)
        {
            var response = await _httpClient.GetAsync("https://api.openbrewerydb.org/v1/breweries?by_name=name");
            response.EnsureSuccessStatusCode();
            if (!response.IsSuccessStatusCode)
            {
                // Map 4xx/5xx to UpstreamApiException with status and body
                throw new UpstreamApiException((int)response.StatusCode, $"Upstream returned {(int)response.StatusCode}", response.ReasonPhrase);
            }
            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<IEnumerable<Brewery>>(json,
             new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
             ?? Enumerable.Empty<Brewery>();
        }

        //UnComment this method for SQLLite and inject BreweryDbContext as DI
        //public async Task<IEnumerable<Brewery>> GetBreweriesAsync()
        //{
        //    return await _context.Breweries.ToListAsync();
        //}
        public async Task<IEnumerable<Brewery>> SearchBreweriesAsync(string query)
        {
           
                // Open Brewery DB supports autocomplete via /breweries/autocomplete
                var response = await _httpClient.GetAsync($"https://api.openbrewerydb.org/v1/breweries/autocomplete?query={query}");
                response.EnsureSuccessStatusCode();
                if (!response.IsSuccessStatusCode)
                {
                    // Map 4xx/5xx to UpstreamApiException with status and body
                    throw new UpstreamApiException((int)response.StatusCode, $"Upstream returned {(int)response.StatusCode}", response.ReasonPhrase);
                }
              var json = await response.Content.ReadAsStringAsync();

                var breweries = JsonSerializer.Deserialize<List<Brewery>>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                
                return breweries ?? new List<Brewery>();
            
        }
    }
}

