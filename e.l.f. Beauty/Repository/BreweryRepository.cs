using System;
using e.l.f._Beauty.Models;
using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace e.l.f._Beauty.Repository
{
    public class BreweryRepository : IBreweryRepository
    {
        private readonly HttpClient _httpClient;
        private readonly IMemoryCache _cache;
        private readonly ILogger<BreweryRepository> _logger;
        public BreweryRepository(HttpClient httpClient, IMemoryCache cache, ILogger<BreweryRepository> logger)
        { _httpClient = httpClient; _cache = cache;_logger = logger; }

        public async Task<IEnumerable<Brewery>> GetBreweriesAsync()
        {
            try
            {
                var response = await _httpClient.GetFromJsonAsync<IEnumerable<Brewery>>(
                    "https://api.openbrewerydb.org/v1/breweries");
                return response ?? Enumerable.Empty<Brewery>();
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex, "Error fetching breweries from external API.");
                throw; // bubble up to global handler
            }
        }
        //UnComment this method for SQLLite and inject BreweryDbContext as DI
        //public async Task<IEnumerable<Brewery>> GetBreweriesAsync()
        //{
        //    return await _context.Breweries.ToListAsync();
        //}
        public async Task<IEnumerable<Brewery>> SearchBreweriesAsync(string query)
        {
            try
            {
                // Cache key based on query
                var cacheKey = $"brewery_autocomplete_{query}";

                if (_cache.TryGetValue(cacheKey, out IEnumerable<Brewery> cachedBreweries))
                {
                    return cachedBreweries;
                }
                // Open Brewery DB supports autocomplete via /breweries/autocomplete
                var response = await _httpClient.GetAsync($"https://api.openbrewerydb.org/v1/breweries/autocomplete?query={query}");
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();

                var breweries = JsonSerializer.Deserialize<List<Brewery>>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                _cache.Set(cacheKey, breweries, TimeSpan.FromMinutes(10));
                return breweries ?? new List<Brewery>();
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex, "Error fetching breweries from external API.");
                throw; // bubble up to global handler
            }
        }
    }
}

