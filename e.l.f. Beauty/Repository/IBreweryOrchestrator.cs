using e.l.f._Beauty.Models;

namespace e.l.f._Beauty.Repository
{
    public interface IBreweryOrchestrator
    {
        Task<IEnumerable<Brewery>> GetBreweriesAsync(BreweryQueryOptions options);
        Task<IEnumerable<Brewery?>> GetBreweryByNameAsync(string name);
        Task AddBreweryAsync(Brewery brewery);
        Task<BulkInsertResult> AddBreweriesAsync(IEnumerable<Brewery> breweries);
        Task<IEnumerable<Brewery>> SearchBreweriesAsync(string query);
    }
}
