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

        public async Task<IEnumerable<Brewery>> GetBreweriesAsync()
        {
            return await _context.Breweries.AsNoTracking().ToListAsync();
        }

        public async Task<IEnumerable<Brewery?>> GetBreweryByNameAsync(string name)
        {
            return await _context.Breweries.AsNoTracking().ToListAsync();
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

        public Task<IEnumerable<Brewery>> SearchBreweriesAsync(string query)
        {
            throw new NotImplementedException();
        }
    }
}
