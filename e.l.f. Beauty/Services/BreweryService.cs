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
                // Create a cache key that includes the relevant query options so different
                // requests (page, pageSize, sort, search, city, and user coords) do not
                // collide and return stale/incorrect data. Only include normalized values.
                options ??= new BreweryQueryOptions();
                string Norm(string? s) => string.IsNullOrWhiteSpace(s) ? string.Empty : Uri.EscapeDataString(s.Trim().ToLowerInvariant());
                string NormDouble(double d) => d.ToString("F4", System.Globalization.CultureInfo.InvariantCulture);

                var keyParts = new List<string>
                {
                    $"page={Math.Max(1, options.Page)}",
                    $"pageSize={Math.Clamp(options.PageSize, 1, 100)}",
                    $"sortBy={Norm(options.SortBy)}",
                    $"asc={options.Ascending}",
                    $"search={Norm(options.Search)}",
                    $"city={Norm(options.City)}",
                };
                if (options.UserLat.HasValue && options.UserLng.HasValue)
                {
                    // Round user-provided coordinates to 4 decimal places to avoid excessive key fragmentation
                    keyParts.Add($"ulat={NormDouble(options.UserLat.Value)}");
                    keyParts.Add($"ulng={NormDouble(options.UserLng.Value)}");
                }

                var cacheKey = "breweries:" + string.Join("&", keyParts);

                // Determine whether the repository can provide a total count.
                // If it can, assume the repository also handled paging and trust
                // its returned slice. If it cannot (returns null) the service
                // will fetch a bounded prefix and apply final paging itself.
                var repoTotal = await _repository.GetTotalCountAsync(options);

                IEnumerable<Brewery> breweries;

                if (repoTotal.HasValue)
                {
                    // Repository supports totals (EF-backed). Request the exact
                    // page from repository and treat the returned items as already
                    // paged. Cache the repository response keyed by the full options
                    // including page/pageSize.
                    breweries = await _cache.GetOrFetchAsync(cacheKey, () => _repository.GetBreweriesAsync(options));
                }
                else
                {
                    // Repository does not support totals (upstream or in-memory that
                    // opts out). Request a bounded prefix (page 1) large enough to
                    // contain requested page then apply paging in the service.
                    var repoFetchOptions = new BreweryQueryOptions
                    {
                        Search = options.Search,
                        City = options.City,
                        SortBy = options.SortBy,
                        Ascending = options.Ascending,
                        UserLat = options.UserLat,
                        UserLng = options.UserLng,
                        Page = 1,
                        PageSize = Math.Clamp(options.Page * options.PageSize, 1, 100)
                    };

                    var repoCacheKey = "breweries:" + string.Join("&", new List<string>
                    {
                        $"page={repoFetchOptions.Page}",
                        $"pageSize={repoFetchOptions.PageSize}",
                        $"sortBy={Norm(options.SortBy)}",
                        $"asc={options.Ascending}",
                        $"search={Norm(options.Search)}",
                        $"city={Norm(options.City)}",
                    });

                    breweries = await _cache.GetOrFetchAsync(repoCacheKey, () => _repository.GetBreweriesAsync(repoFetchOptions));
                }

                // Apply any additional in-memory filtering/sorting that the repository
                // could not perform. Note: because cache keys include the options, this
                // avoids returning unrelated cached datasets.
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

