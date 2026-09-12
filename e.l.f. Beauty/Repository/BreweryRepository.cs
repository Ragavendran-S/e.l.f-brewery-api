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
        
        public BreweryRepository(HttpClient httpClient)
        { _httpClient = httpClient; }

        public async Task<IEnumerable<Brewery>> GetBreweriesAsync()
        {
            var response = await _httpClient.GetAsync("https://api.openbrewerydb.org/breweries");
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<IEnumerable<Brewery>>(json) ?? Enumerable.Empty<Brewery>();
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

                var json = await response.Content.ReadAsStringAsync();

                var breweries = JsonSerializer.Deserialize<List<Brewery>>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                
                return breweries ?? new List<Brewery>();
            
        }
    }
}

