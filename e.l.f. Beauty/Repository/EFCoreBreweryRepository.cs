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

        public Task<IEnumerable<Brewery>> SearchBreweriesAsync(string query)
        {
            throw new NotImplementedException();
        }
    }
}
