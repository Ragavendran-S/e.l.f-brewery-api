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
        private readonly IUpstreamBreweryClient _upstream;
        private readonly BreweryDbContext? _dbContext;
        private readonly ILogger<BreweryRepository> _logger;

        public BreweryRepository(IUpstreamBreweryClient upstream, BreweryDbContext? dbContext, ILogger<BreweryRepository> logger)
        {
            _upstream = upstream ?? throw new ArgumentNullException(nameof(upstream));
            _dbContext = dbContext;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<BulkInsertResult> AddBreweriesAsync(IEnumerable<Brewery> breweries)
        {
            if (breweries == null) throw new ArgumentNullException(nameof(breweries));

            if (_dbContext != null)
            {
                _dbContext.Breweries.AddRange(breweries);
                await _dbContext.SaveChangesAsync();
                return new BulkInsertResult
                {
                    Total = breweries.Count(),
                    SuccessCount = breweries.Count(),
                    FailedCount = 0
                };
            }

            // Fall back to posting each item upstream (best-effort) but avoid tight-loop DB inserts.
            var result = new BulkInsertResult { Total = breweries.Count() };
            foreach (var brewery in breweries)
            {
                try
                {
                    var response = await _upstream.PostBreweryAsync(brewery);
                    if (!response.IsSuccessStatusCode)
                    {
                        _logger.LogWarning("Upstream POST for brewery {Id} returned status {Status}", brewery.Id, response.StatusCode);
                        result.FailedCount++;
                        result.Failures.Add(new BulkInsertFailure { BreweryId = brewery.Id, Reason = $"Status {response.StatusCode}" });
                        continue;
                    }
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

            // If a DbContext is available, persist to the local database.
            if (_dbContext != null)
            {
                _dbContext.Breweries.Add(brewery);
                await _dbContext.SaveChangesAsync();
                return;
            }

            // Otherwise, try to POST to the upstream API if supported (best-effort).
            try
            {
                var response = await _upstream.PostBreweryAsync(brewery);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Upstream POST for brewery {Id} returned status {Status}", brewery.Id, response.StatusCode);
                    return;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to POST brewery {Id} to upstream API", brewery.Id);
                throw;
            }
        }

        public async Task<IEnumerable<Brewery>> GetBreweriesAsync(BreweryQueryOptions options)
        {
            // If a DbContext is available, perform DB-backed query with paging to avoid loading everything.
            if (_dbContext != null)
            {
                var query = _dbContext.Breweries.AsNoTracking().AsQueryable();

                // Apply simple search/city filters that repository can handle efficiently
                if (!string.IsNullOrWhiteSpace(options?.Search))
                    query = query.Where(b => EF.Functions.Like(b.Name, $"%{options.Search}%"));
                if (!string.IsNullOrWhiteSpace(options?.City))
                    query = query.Where(b => b.City == options.City);

                // Apply sorting if available
                if (!string.IsNullOrWhiteSpace(options?.SortBy))
                {
                    if (options.SortBy.Equals("Name", StringComparison.OrdinalIgnoreCase))
                        query = options.Ascending ? query.OrderBy(b => b.Name) : query.OrderByDescending(b => b.Name);
                    else if (options.SortBy.Equals("City", StringComparison.OrdinalIgnoreCase))
                        query = options.Ascending ? query.OrderBy(b => b.City) : query.OrderByDescending(b => b.City);
                }

                // Use the injected paging helper by resolving IPagingHelper from current service provider is not available here.
                // Instead apply simple paging logic to the IQueryable to keep DB-side pagination.
                var page = options?.Page ?? 1;
                var pageSize = options?.PageSize ?? 10;
                if (page < 1) page = 1;
                if (pageSize < 1) pageSize = 10;
                if (pageSize > 100) pageSize = 100;

                var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
                return items;
            }

            // Fallback to upstream client when no DB is available.
            return await _upstream.GetBreweriesAsync(options);
        }

        public async Task<IEnumerable<Brewery?>> GetBreweryByNameAsync(string name)
        {
            return await _upstream.GetBreweryByNameAsync(name);
        }

        //UnComment this method for SQLLite and inject BreweryDbContext as DI
        //public async Task<IEnumerable<Brewery>> GetBreweriesAsync()
        //{
        //    return await _context.Breweries.ToListAsync();
        //}
        public async Task<IEnumerable<Brewery>> SearchBreweriesAsync(string query)
        {
            // Delegate autocomplete/search to the upstream client which encapsulates HTTP and mapping concerns.
            return await _upstream.SearchBreweriesAsync(query);
        }
    }
}

