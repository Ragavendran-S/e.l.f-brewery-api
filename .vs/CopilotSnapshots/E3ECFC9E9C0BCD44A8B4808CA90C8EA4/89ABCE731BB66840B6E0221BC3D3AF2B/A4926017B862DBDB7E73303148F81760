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
        
        public BreweryRepository(HttpClient httpClient)
        { _httpClient = httpClient; }

        public Task AddBreweryAsync(Brewery brewery)
        {
            throw new NotImplementedException();
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

