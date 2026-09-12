using System;
using e.l.f._Beauty.Models;
using Microsoft.EntityFrameworkCore;

namespace e.l.f._Beauty.Repository
{
    public class EfCoreBreweryRepository : IBreweryRepository
    {
        private readonly BreweryDbContext _context;
        public EfCoreBreweryRepository(BreweryDbContext context) => _context = context;

        public async Task<IEnumerable<Brewery>> GetBreweriesAsync()
        {
            return await _context.Breweries.AsNoTracking().ToListAsync();
        }

        public async Task<IEnumerable<Brewery>> SearchBreweriesAsync(string query)
        {
            throw new NotImplementedException();
        }
    }
}

