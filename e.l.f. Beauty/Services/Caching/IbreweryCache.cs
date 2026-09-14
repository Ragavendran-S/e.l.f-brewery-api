using e.l.f._Beauty.Models;

public interface IBreweryCache
{
    Task<IEnumerable<Brewery>> GetOrFetchAsync(string key, Func<Task<IEnumerable<Brewery>>> fetch);
}