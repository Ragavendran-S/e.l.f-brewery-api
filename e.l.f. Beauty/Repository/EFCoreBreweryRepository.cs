using e.l.f._Beauty.Models;
using e.l.f._Beauty.Repository;
using ElfBreweryApi.Repositories;
//using ElfBreweryApi.Models;
using Microsoft.EntityFrameworkCore;

namespace ElfBreweryApi.Repositories
{
    public class EfCoreBreweryRepository : IBreweryRepository
    {
        private readonly BreweryDbContext _context;

        public EfCoreBreweryRepository(BreweryDbContext context)
        {
            _context = context;
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

            var page = options?.Page ?? 1;
            var pageSize = options?.PageSize ?? 10;
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;
            if (pageSize > 100) pageSize = 100;

            var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
            return items;
        }

        public async Task<IEnumerable<Brewery?>> GetBreweryByNameAsync(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return Enumerable.Empty<Brewery?>();

            // Search by name (case-insensitive, contains). Return any matching breweries.
            var pattern = $"%{name}%";
            var results = await _context.Breweries
                .AsNoTracking()
                .Where(b => EF.Functions.Like(b.Name, pattern))
                .ToListAsync();

            return results.Cast<Brewery?>();
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
    }
}
