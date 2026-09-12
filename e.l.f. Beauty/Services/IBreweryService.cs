using System.Threading.Tasks;
using e.l.f._Beauty.Models;

namespace e.l.f._Beauty.Services
{
    public interface IBreweryService
    {
        Task <PagedResult<Brewery>> GetBreweriesAsync(BreweryQueryOptions options);
        Task<IEnumerable<Brewery>> AutocompleteAsync(string query);
        Task<IEnumerable<Brewery>> SearchBreweriesAsync(string query);
        
    }
}