using System;
using e.l.f._Beauty.Models;
using e.l.f._Beauty.Repository;
using Microsoft.EntityFrameworkCore;
//using Internal;
using Microsoft.Extensions.Caching.Memory;

namespace e.l.f._Beauty.Services
{
    public class BreweryService : IBreweryService
    {
        private readonly IBreweryRepository _repository;
        private readonly IMemoryCache _cache;
        private readonly ILogger<BreweryService> _logger;

        public BreweryService(IBreweryRepository repository, IMemoryCache cache, ILogger<BreweryService> logger)
        {
            _repository = repository;
            _cache = cache;
            _logger = logger;
        }

        //public BreweryService(IBreweryRepository repository, IMemoryCache cache)
        //{
        //    _repository = repository;
        //    _cache = cache;
        //}
        public async Task<IEnumerable<Brewery>> AutocompleteAsync(string query)
        {
            return await _repository.SearchBreweriesAsync(query);
        }
        public async Task<PagedResult<Brewery>> GetBreweriesAsync(BreweryQueryOptions options)
        {
            try
            {
                // Cache key can be dynamic if you want per-query caching
                var cacheKey = "breweries";

                // Try to get breweries from cache
                var breweries = await _cache.GetOrCreateAsync(cacheKey, async entry =>
                {
                    // Set cache expiration to 10 minutes
                    entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10);

                    _logger.LogInformation("Cache miss for {CacheKey}. Fetching breweries from repository at {Time}.", cacheKey, DateTime.UtcNow);

                    return await _repository.GetBreweriesAsync();

                });

                // Materialize to list
                breweries = breweries.ToList();

                // Search filter
                
                    if (!string.IsNullOrEmpty(options.Search))
                    {
                        _logger.LogInformation("Applying search filter: {Search}", options.Search);

                        breweries = breweries
                            .Where(b => b.Name != null &&
                                        b.Name.Contains(options.Search, StringComparison.OrdinalIgnoreCase))
                            .ToList();
                    }
                    // City filter
                    if (!string.IsNullOrEmpty(options.City))
                    {
                        _logger.LogInformation("Filtering by city: {City}", options.City);

                        breweries = breweries
                            .Where(b => b.City != null &&
                                        b.City.Equals(options.City, StringComparison.OrdinalIgnoreCase))
                            .ToList();
                    }
                    // Sorting
                    _logger.LogInformation("Sorting by {SortBy}, Ascending: {Ascending}", options.SortBy, options.Ascending);

                    breweries = options.SortBy switch
                    {
                        "City" => options.Ascending
                            ? breweries.OrderBy(b => b.City ?? string.Empty).ToList()
                            : breweries.OrderByDescending(b => b.City ?? string.Empty).ToList(),

                        "Distance" => SortByDistance(breweries, options).ToList(),

                        _ => options.Ascending
                            ? breweries.OrderBy(b => b.Name ?? string.Empty).ToList()
                            : breweries.OrderByDescending(b => b.Name ?? string.Empty).ToList()
                    };
                    // Paging
                    var totalItems = breweries.Count();
                    var items = breweries
                        .Skip((options.Page - 1) * options.PageSize)
                        .Take(options.PageSize)
                        .ToList();

                    _logger.LogInformation("Returning {Count} items out of {Total} total for Page {Page} with PageSize {PageSize}.",
                        items.Count, totalItems, options.Page, options.PageSize);
                    return new PagedResult<Brewery>(items, totalItems, options.Page, options.PageSize);
                
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex, "Error fetching breweries from external API.");
                throw; // bubble up to global handler
            }
        }




        private IEnumerable<Brewery> SortByDistance(IEnumerable<Brewery> breweries, BreweryQueryOptions options)
        {
            if (options.UserLat == null || options.UserLng == null) return breweries;
            return options.Ascending
                ? breweries.OrderBy(b => CalculateDistance(options.UserLat.Value, options.UserLng.Value, b.Latitude, b.Longitude))
                : breweries.OrderByDescending(b => CalculateDistance(options.UserLat.Value, options.UserLng.Value, b.Latitude, b.Longitude));
        }

        private double CalculateDistance(double lat1, double lon1, double? lat2, double? lon2)
        {
            if (lat2 == null || lon2 == null) return double.MaxValue;
            var dLat = (lat2.Value - lat1) * Math.PI / 180.0;
            var dLon = (lon2.Value - lon1) * Math.PI / 180.0;
            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(lat1 * Math.PI / 180.0) * Math.Cos(lat2.Value * Math.PI / 180.0) *
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return 6371 * c; // Earth radius in km
        }

        
    }


}

