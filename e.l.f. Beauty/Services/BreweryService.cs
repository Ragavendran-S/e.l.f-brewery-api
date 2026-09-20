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
        private readonly IBreweryCache _cache;
        private readonly ILogger<BreweryService> _logger;
        private readonly IMapper _mapper;
        private readonly IBreweryFilter _filter;
        private readonly IBrewerySorterFactory _sorterFactory;
        private readonly IPagingHelper _paging;

        public BreweryService(IBreweryRepository repository, IBreweryCache cache, ILogger<BreweryService> logger,IMapper mapper,IBreweryFilter filter, IBrewerySorterFactory sorterFactory, IPagingHelper paging)
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
                var breweries = await _cache.GetOrFetchAsync("breweries", () => _repository.GetBreweriesAsync(options));

                breweries = _filter.Apply(breweries, options);

                var sorter = _sorterFactory.GetSorter(options.SortBy ?? string.Empty);
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
            // Fetch from repository or external API
            var key = $"search:{query}";
            // Fetch search results from repository (which may call an upstream autocomplete endpoint).
            // Do NOT re-filter results here to avoid duplicating upstream filtering logic; repository
            // implementations are expected to return already-filtered results for the query.
            var externalResults = await _cache.GetOrFetchAsync(key, () => _repository.SearchBreweriesAsync(query));
            // Map external → internal and return as-is
            var mappedResults = _mapper.Map<IEnumerable<Brewery>>(externalResults);
            return mappedResults;
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

        public async Task<IEnumerable<Brewery>> AutocompleteAsync(string query)
        {
            var key = $"autocomplete:{query}";
            // Use per-query cache key so different autocomplete queries do not collide with
            // the general breweries list cache. Fetch from repository.SearchBreweriesAsync
            // (which may call an upstream autocomplete API) rather than loading the entire
            // breweries list and filtering locally.
            var externalResults = await _cache.GetOrFetchAsync(key, () => _repository.SearchBreweriesAsync(query));
            var mappedResults = _mapper.Map<IEnumerable<Brewery>>(externalResults);
            return mappedResults;
        }
        
    }


}

