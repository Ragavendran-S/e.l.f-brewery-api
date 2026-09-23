using System.Text.Json;
using e.l.f._Beauty.Models;
using Microsoft.Extensions.Caching.Memory;

public interface IBreweryRepository
{
    Task<IEnumerable<Brewery>> GetBreweriesAsync(BreweryQueryOptions options);
    // Returns total number of items that match the provided query filters when
    // available. Implementations that cannot cheaply provide a total (for
    // example upstream HTTP clients) may return null to indicate the value is
    // unknown.
    Task<int?> GetTotalCountAsync(BreweryQueryOptions options);
    Task<IEnumerable<Brewery>> SearchBreweriesAsync(string query);
    Task<IEnumerable<Brewery?>> GetBreweryByNameAsync(string name);
    Task AddBreweryAsync(Brewery brewery);

    // Efficient bulk insert for EF Core implementations. Implementations that
    // do not support bulk operations may fall back to per-item behavior.
    // Returns a summary result with counts and any failures recorded.
    Task<BulkInsertResult> AddBreweriesAsync(IEnumerable<Brewery> breweries);
}

public class BulkInsertResult
{
    public int Total { get; set; }
    public int SuccessCount { get; set; }
    public int FailedCount { get; set; }
    public List<BulkInsertFailure> Failures { get; set; } = new();
}

public class BulkInsertFailure
{
    public string? BreweryId { get; set; }
    public string? Reason { get; set; }
}


