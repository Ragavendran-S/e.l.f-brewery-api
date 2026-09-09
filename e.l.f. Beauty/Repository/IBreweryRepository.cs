using System.Text.Json;
using e.l.f._Beauty.Models;
using Microsoft.Extensions.Caching.Memory;

public interface IBreweryRepository
{
    Task<IEnumerable<Brewery>> GetBreweriesAsync();
    Task<IEnumerable<Brewery>> SearchBreweriesAsync(string query);
}


