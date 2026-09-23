using e.l.f._Beauty.Models;
using e.l.f._Beauty.Repository;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace e.l.f._Beauty.Repository
{
    public class EfCoreBreweryRepository : IBreweryRepository
    {
        private readonly BreweryDbContext _context;
        private readonly IUpstreamBreweryClient? _upstream;

        // EF repository applies paging within query methods and will fall back to the
        // upstream client when the local DB table contains no rows. Dependencies are
        // resolved via DI so Program.cs should resolve the concrete type from the
        // service provider rather than constructing it manually.
        public EfCoreBreweryRepository(BreweryDbContext context, IUpstreamBreweryClient upstream)
        {
            _context = context;
            _upstream = upstream ?? throw new ArgumentNullException(nameof(upstream));
        }

        // Back-compat overload used by tests and older code paths that construct the
        // repository directly without providing an upstream client. When upstream is
        // not available the repository will behave like a pure EF-backed store and
        // will not attempt to fall back to the upstream API.
        public EfCoreBreweryRepository(BreweryDbContext context)
        {
            _context = context;
            _upstream = null;
        }

        public async Task<IEnumerable<Brewery>> GetBreweriesAsync(BreweryQueryOptions options)
        {
            var query = _context.Breweries.AsNoTracking().AsQueryable();

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

            // Normalize options to a non-null local instance to satisfy nullable analysis
            var localOptions = options ?? new BreweryQueryOptions();
            var page = localOptions.Page;
            var pageSize = localOptions.PageSize;
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;
            if (pageSize > 100) pageSize = 100;

            // If the repository was asked to return a bounded prefix (page=1 with
            // pageSize > actual pageSize) honor that and return the requested
            // limited slice. Consumers (service layer) may request a larger
            // prefix to avoid double-pagination; when page==1 return up to pageSize
            // items. If callers request page>1 directly against EF repository we
            // still perform normal skip/take semantics.
            // If caller provided an explicit options instance that requests page 1
            // but a different PageSize, respect that bounded-prefix request.
            var requestedPage = options?.Page ?? localOptions.Page;
            var requestedPageSize = options?.PageSize;
            if (requestedPage == 1 && requestedPageSize.HasValue && requestedPageSize.Value != pageSize)
            {
                page = 1;
                pageSize = requestedPageSize.Value;
                if (pageSize < 1) pageSize = 10;
                if (pageSize > 100) pageSize = 100;
            }

            // If the database table is empty, fall back to the upstream API so the
            // running service can still return useful data even when the local DB
            // hasn't been seeded.
            var hasAny = await _context.Breweries.AnyAsync();
            if (!hasAny)
            {
                if (_upstream != null)
                {
                    // When the local DB is empty attempt to retrieve data from the
                    // upstream API and seed the local table so subsequent reads come
                    // from the DB (DB-first). Limit behavior to the upstream client
                    // response for the provided options; do not attempt to page the
                    // entire upstream dataset here.
                    var upstreamItems = (await _upstream.GetBreweriesAsync(options))?.ToList() ?? new List<Brewery>();
                    if (upstreamItems.Any())
                    {
                        // Add range and commit once for efficiency. Use AddRange which
                        // will set entity states appropriately.
                        // Ensure non-nullable string properties are not null to avoid
                        // database NOT NULL constraint violations when upstream returns
                        // missing fields (some upstream responses contain nulls).
                        foreach (var b in upstreamItems)
                        {
                            if (b == null) continue;
                            b.Id = b.Id ?? string.Empty;
                            b.Name = b.Name ?? string.Empty;
                            b.Brewery_Type = b.Brewery_Type ?? string.Empty;
                            b.Street = b.Street ?? string.Empty;
                            b.City = b.City ?? string.Empty;
                            b.State = b.State ?? string.Empty;
                            b.Country = b.Country ?? string.Empty;
                            b.Phone = b.Phone ?? string.Empty;
                        }
                        // Remove duplicates by Id to avoid EF tracking conflicts when upstream returns duplicate entries
                        var uniqueItems = upstreamItems
                            .Where(b => b != null)
                            .GroupBy(b => b.Id)
                            .Select(g => g.First())
                            .ToList();
                        _context.Breweries.AddRange(uniqueItems);
                        await _context.SaveChangesAsync();
                        return uniqueItems;
                    }
                    return Enumerable.Empty<Brewery>();
                }

                return Enumerable.Empty<Brewery>();
            }

            var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
            return items;
        }

        public async Task<int?> GetTotalCountAsync(BreweryQueryOptions options)
        {
            var query = _context.Breweries.AsNoTracking().AsQueryable();
            if (!string.IsNullOrWhiteSpace(options?.Search))
                query = query.Where(b => EF.Functions.Like(b.Name, $"%{options.Search}%"));
            if (!string.IsNullOrWhiteSpace(options?.City))
                query = query.Where(b => b.City == options.City);

            return await query.CountAsync();
        }

        public bool SupportsServerSideFiltering() => true;

        public async Task<IEnumerable<Brewery?>> GetBreweryByNameAsync(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return Enumerable.Empty<Brewery?>();

            // Search by name (case-insensitive, contains). If DB is empty delegate
            // to upstream so callers receive results from the public API instead of
            // an empty set.
            var hasAny = await _context.Breweries.AnyAsync();
            if (!hasAny)
            {
                if (_upstream != null)
                {
                    var upstream = await _upstream.GetBreweryByNameAsync(name);
                    return upstream.Cast<Brewery?>();
                }

                return Enumerable.Empty<Brewery?>();
            }

            var pattern = $"%{name}%";
            var results = await _context.Breweries
                .AsNoTracking()
                .Where(b => EF.Functions.Like(b.Name, pattern))
                .ToListAsync();

            if (results.Any())
                return results.Cast<Brewery?>();

            // If no local matches, try upstream by-name first, then broader search.
            if (_upstream != null)
            {
                var upstream = await _upstream.GetBreweryByNameAsync(name);
                if (upstream.Any())
                    return upstream.Cast<Brewery?>();

                var search = await _upstream.SearchBreweriesAsync(name);
                return search.Cast<Brewery?>();
            }

            return Enumerable.Empty<Brewery?>();
        }

        public async Task AddBreweryAsync(Brewery brewery)
        {
            _context.Breweries.Add(brewery);
            await _context.SaveChangesAsync();
        }

        public async Task<BulkInsertResult> AddBreweriesAsync(IEnumerable<Brewery> breweries)
        {
            if (breweries == null) throw new ArgumentNullException(nameof(breweries));

            // Use AddRange for efficient batching and commit once.
            _context.Breweries.AddRange(breweries);
            await _context.SaveChangesAsync();
            return new BulkInsertResult
            {
                Total = breweries.Count(),
                SuccessCount = breweries.Count(),
                FailedCount = 0
            };
        }

        public async Task<IEnumerable<Brewery>> SearchBreweriesAsync(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return Enumerable.Empty<Brewery>();

            // If DB empty, delegate to upstream autocomplete/search endpoint.
            var hasAny = await _context.Breweries.AnyAsync();
            if (!hasAny)
            {
                if (_upstream != null)
                    return await _upstream.SearchBreweriesAsync(query);
                return Enumerable.Empty<Brewery>();
            }

            var pattern = $"%{query}%";

            // Search name or city for autocomplete/search behavior. Limit results for performance.
            var results = await _context.Breweries
                .AsNoTracking()
                .Where(b => EF.Functions.Like(b.Name, pattern) || EF.Functions.Like(b.City, pattern))
                .OrderBy(b => b.Name)
                .Take(25)
                .ToListAsync();

            return results;
        }

        // (No additional compatibility wrapper required)
    }
}
