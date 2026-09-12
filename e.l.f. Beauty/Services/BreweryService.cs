using System;
using AutoMapper;
using e.l.f._Beauty.Models;
using e.l.f._Beauty.Repository;
using Microsoft.EntityFrameworkCore;
//using Internal;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.AspNetCore.WebUtilities;

namespace e.l.f._Beauty.Services
{
    public class BreweryService : IBreweryService
    {
        private readonly IBreweryRepository _repository;
        private readonly IMemoryCache _cache;
        private readonly ILogger<BreweryService> _logger;
        private readonly IMapper _mapper;
        private readonly IBreweryFilter _filter;
        private readonly IBrewerySorterFactory _sorterFactory;
        private readonly IPagingHelper _paging;

        public BreweryService(IBreweryRepository repository, IMemoryCache cache, ILogger<BreweryService> logger,IMapper mapper,IBreweryFilter filter, IBrewerySorterFactory sorterFactory, IPagingHelper paging)
        {
            _repository = repository;
            _cache = cache;
            _logger = logger;
            _mapper = mapper;
            _filter = filter;
            _sorterFactory = sorterFactory;
            _paging = paging;
        }

        public async Task<PagedResult<Brewery>> GetBreweriesAsync(BreweryQueryOptions options)
        {
            try
            {
                var breweries = await _cache.GetOrFetchAsync("breweries", () => _repository.GetBreweriesAsync());

                breweries = _filter.Apply(breweries, options);

                var sorter = _sorterFactory.GetSorter(options.SortBy);
                breweries = sorter.Sort(breweries, options);

                var result = _paging.Apply(breweries, options);

                _logger.LogInformation("Returning {Count} items out of {Total} total for Page {Page} with PageSize {PageSize}.",
                    result.Items.Count, result.TotalItems, result.Page, result.PageSize);

                return result;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex, "Error fetching breweries from external API.");
                throw;
            }
        }
        public async Task<IEnumerable<Brewery>> SearchBreweriesAsync(string query)
        {
            //For Internal Demo - Start
            //simulate External API call
            //var externalResults = new List<ExternalBrewery>
            //{
            //    new ExternalBrewery { brewery_id = "123", brewery_name = "Lagunitas Brewing Co", location_city = "Petaluma", location_state = "California", location_country = "USA" },
            //    new ExternalBrewery { brewery_id = "456", brewery_name = "Lager House", location_city = "Detroit", location_state = "Michigan", location_country = "USA" }
            //};
            //For Internal Demo - End
            // Fetch from repository or external API
            var externalResults = await _repository.SearchBreweriesAsync(query);
                    // Map external → internal
                    var mappedResults = _mapper.Map<IEnumerable<Brewery>>(externalResults);

            return mappedResults.Where(b =>
            !string.IsNullOrEmpty(b.Name) &&
            b.Name.StartsWith(query, StringComparison.OrdinalIgnoreCase));

            //return await _repository.SearchBreweriesAsync(query);
        }
        //public async Task<PagedResult<Brewery>> GetBreweriesAsync(BreweryQueryOptions options)
        //{
        //    try
        //    {
        //        // Cache key can be dynamic if you want per-query caching
        //        var cacheKey = "breweries";

        //        // Try to get breweries from cache
        //        var breweries = await _cache.GetOrCreateAsync(cacheKey, async entry =>
        //        {
        //            // Set cache expiration to 10 minutes
        //            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10);

        //            _logger.LogInformation("Cache miss for {CacheKey}. Fetching breweries from repository at {Time}.", cacheKey, DateTime.UtcNow);

        //            return await _repository.GetBreweriesAsync();

        //        });

        //        // Materialize to list
        //        breweries = breweries.ToList();

        //        // Search filter
                
        //            if (!string.IsNullOrEmpty(options.Search))
        //            {
        //                _logger.LogInformation("Applying search filter: {Search}", options.Search);

        //                breweries = breweries
        //                    .Where(b => b.Name != null &&
        //                                b.Name.Contains(options.Search, StringComparison.OrdinalIgnoreCase))
        //                    .ToList();
        //            }
        //            // City filter
        //            if (!string.IsNullOrEmpty(options.City))
        //            {
        //                _logger.LogInformation("Filtering by city: {City}", options.City);

        //                breweries = breweries
        //                    .Where(b => b.City != null &&
        //                                b.City.Equals(options.City, StringComparison.OrdinalIgnoreCase))
        //                    .ToList();
        //            }
        //            // Sorting
        //            _logger.LogInformation("Sorting by {SortBy}, Ascending: {Ascending}", options.SortBy, options.Ascending);

        //            breweries = options.SortBy switch
        //            {
        //                "City" => options.Ascending
        //                    ? breweries.OrderBy(b => b.City ?? string.Empty).ToList()
        //                    : breweries.OrderByDescending(b => b.City ?? string.Empty).ToList(),

        //                "Distance" => SortByDistance(breweries, options).ToList(),

        //                _ => options.Ascending
        //                    ? breweries.OrderBy(b => b.Name ?? string.Empty).ToList()
        //                    : breweries.OrderByDescending(b => b.Name ?? string.Empty).ToList()
        //            };
        //            // Paging
        //            var totalItems = breweries.Count();
        //            var items = breweries
        //                .Skip((options.Page - 1) * options.PageSize)
        //                .Take(options.PageSize)
        //                .ToList();

        //            _logger.LogInformation("Returning {Count} items out of {Total} total for Page {Page} with PageSize {PageSize}.",
        //                items.Count, totalItems, options.Page, options.PageSize);
        //            return new PagedResult<Brewery>(items, totalItems, options.Page, options.PageSize);
                
        //    }
        //    catch (HttpRequestException ex)
        //    {
        //        _logger.LogWarning(ex, "Error fetching breweries from external API.");
        //        throw; // bubble up to global handler
        //    }
        //}




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

        public async Task<IEnumerable<Brewery>> AutocompleteAsync(string query)
        {
            var breweries = await _repository.GetBreweriesAsync();

            var results = breweries
                .Where(b => !string.IsNullOrEmpty(b.Name) &&
                            b.Name.StartsWith(query, StringComparison.OrdinalIgnoreCase));

            return results;
        }
        
    }


}

