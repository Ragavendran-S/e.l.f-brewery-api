using System;
using System.Linq;
using System.Collections.Generic;
using e.l.f._Beauty.Models;
using e.l.f._Beauty.Services;
using Microsoft.EntityFrameworkCore;

namespace e.l.f._Beauty.Repository
{
    public class BreweryRepository : IBreweryRepository
    {
        private readonly IUpstreamBreweryClient _upstream;
        private readonly BreweryDbContext? _dbContext;
        private readonly ILogger<BreweryRepository> _logger;
        private readonly IPagingHelper? _pagingHelper;

        public BreweryRepository(IUpstreamBreweryClient upstream, ILogger<BreweryRepository> logger)
        {
            _upstream = upstream ?? throw new ArgumentNullException(nameof(upstream));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        // Back-compat constructor used by tests and older DI registrations. When a DbContext and paging helper
        // are provided the repository will perform DB-backed queries; otherwise it delegates to upstream.
        public BreweryRepository(IUpstreamBreweryClient upstream, BreweryDbContext? dbContext, ILogger<BreweryRepository> logger, IPagingHelper pagingHelper)
            : this(upstream, logger)
        {
            _dbContext = dbContext;
            _pagingHelper = pagingHelper ?? throw new ArgumentNullException(nameof(pagingHelper));
        }

        public async Task<BulkInsertResult> AddBreweriesAsync(IEnumerable<Brewery> breweries)
        {
            if (breweries == null) throw new ArgumentNullException(nameof(breweries));

            var result = new BulkInsertResult();

            foreach (var brewery in breweries)
            {
                result.Total++;
                try
                {
                    await _upstream.PostBreweryAsync(brewery);
                    result.SuccessCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to POST brewery {Id} to upstream API; continuing with others", brewery.Id);
                    result.FailedCount++;
                    result.Failures.Add(new BulkInsertFailure { BreweryId = brewery.Id, Reason = ex.Message });
                }
            }

            return result;
        }

        public async Task AddBreweryAsync(Brewery brewery)
        {
            if (brewery == null) throw new ArgumentNullException(nameof(brewery));
            await _upstream.PostBreweryAsync(brewery);
        }

        public async Task<IEnumerable<Brewery>> GetBreweriesAsync(BreweryQueryOptions options)
        {
            // If DbContext is available, use it for DB-backed paging/filtering; otherwise defer to upstream.
            if (_dbContext != null)
            {
                var query = _dbContext.Breweries.AsNoTracking().AsQueryable();
                if (!string.IsNullOrWhiteSpace(options?.Search))
                    query = query.Where(b => EF.Functions.Like(b.Name, $"%{options.Search}%"));
                if (!string.IsNullOrWhiteSpace(options?.City))
                    query = query.Where(b => b.City == options.City);

                if (!string.IsNullOrWhiteSpace(options?.SortBy))
                {
                    if (options.SortBy.Equals("Name", StringComparison.OrdinalIgnoreCase))
                        query = options.Ascending ? query.OrderBy(b => b.Name) : query.OrderByDescending(b => b.Name);
                    else if (options.SortBy.Equals("City", StringComparison.OrdinalIgnoreCase))
                        query = options.Ascending ? query.OrderBy(b => b.City) : query.OrderByDescending(b => b.City);
                }

                var page = options?.Page ?? 1;
                var pageSize = options?.PageSize ?? 10;

                // Use provided paging helper when available; otherwise apply simple LINQ pagination.
                if (_pagingHelper != null)
                {
                    var paged = _pagingHelper.ApplyPaging(query, page, pageSize);
                    var items = await paged.ToListAsync();
                    return items;
                }

                var itemsFallback = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
                return itemsFallback;
            }

            return await _upstream.GetBreweriesAsync(options);
        }

        public async Task<IEnumerable<Brewery?>> GetBreweryByNameAsync(string name)
        {
            // Prefer local DB when available
            if (_dbContext != null)
            {
                if (string.IsNullOrWhiteSpace(name))
                    return Enumerable.Empty<Brewery?>();

                var pattern = $"%{name}%";
                var results = _dbContext.Breweries
                    .AsNoTracking()
                    .Where(b => EF.Functions.Like(b.Name, pattern))
                    .ToList();

                if (results != null && results.Any()) return results.Cast<Brewery?>();
            }

            var result = await _upstream.GetBreweryByNameAsync(name);
            // If upstream by_name lookup returns no results try the search/autocomplete
            // endpoints which may return matches for partial names.
            if (result == null || !result.Any())
            {
                var search = await _upstream.SearchBreweriesAsync(name);
                return search?.Cast<Brewery?>() ?? Enumerable.Empty<Brewery?>();
            }

            // Map upstream "IEnumerable<Brewery>" to "IEnumerable<Brewery?>" expected by the interface
            return result.Cast<Brewery?>();
        }

        public Task<IEnumerable<Brewery>> SearchBreweriesAsync(string query)
        {
            return _upstream.SearchBreweriesAsync(query);
        }
    }
}

