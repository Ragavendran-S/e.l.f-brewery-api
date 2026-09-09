using System;
using e.l.f._Beauty.Models;

namespace e.l.f._Beauty.Repository
{
    public class InMemoryBreweryRepository : IBreweryRepository
    {
        private readonly List<Brewery> _breweries;
        public InMemoryBreweryRepository(List<Brewery> breweries) => _breweries = breweries;

        public Task<IEnumerable<Brewery>> GetBreweriesAsync() => Task.FromResult(_breweries.AsEnumerable());

        public Task<IEnumerable<Brewery>> SearchBreweriesAsync(string query)
        {
            throw new NotImplementedException();
        }
    }
}

