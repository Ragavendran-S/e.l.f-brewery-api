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
        private readonly ILogger<BreweryRepository> _logger;

        public BreweryRepository(HttpClient httpClient, BreweryDbContext? dbContext = null, ILogger<BreweryRepository>? logger = null)
        {
            _httpClient = httpClient;
            _dbContext = dbContext;
            _logger = logger ?? new Microsoft.Extensions.Logging.Abstractions.NullLogger<BreweryRepository>();
        }

        public async Task AddBreweriesAsync(IEnumerable<Brewery> breweries)
        {
            if (breweries == null) throw new ArgumentNullException(nameof(breweries));

            if (_dbContext != null)
            {
                // Delegate to EF Core implementation when a context is available
                _dbContext.Breweries.AddRange(breweries);
                await _dbContext.SaveChangesAsync();
                return;
            }

            // Fall back to posting each item upstream (best-effort) but avoid tight-loop DB inserts.
            foreach (var brewery in breweries)
            {
                try
                {
                    var json = JsonSerializer.Serialize(brewery);
                    using var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
                    var response = await _httpClient.PostAsync("/v1/breweries", content);
                    if (!response.IsSuccessStatusCode)
                    {
                        _logger.LogWarning("Upstream POST for brewery {Id} returned status {Status}", brewery.Id, response.StatusCode);
                    }
                }
                catch (Exception ex)
                {
                    // Log failures for diagnostics but continue with the next item to preserve
                    // best-effort behavior for bulk operations.
                    _logger.LogError(ex, "Failed to POST brewery {Id} to upstream API; continuing with others", brewery.Id);
                }
            }
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
                    _logger.LogWarning("Upstream POST for brewery {Id} returned status {Status}", brewery.Id, response.StatusCode);
                    // Do not throw here to avoid breaking callers when upstream is read-only.
                    return;
                }
            }
            catch (Exception ex)
            {
                // Log and rethrow so callers are aware of persistent failures. Use 'throw;' to
                // preserve the original stack trace rather than 'throw ex'.
                _logger.LogError(ex, "Failed to POST brewery {Id} to upstream API", brewery.Id);
                throw;
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

