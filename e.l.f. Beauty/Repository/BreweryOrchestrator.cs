using e.l.f._Beauty.Models;
using e.l.f._Beauty.Services;
using Microsoft.EntityFrameworkCore;

namespace e.l.f._Beauty.Repository
{
    public class BreweryOrchestrator : IBreweryOrchestrator
    {
        private readonly IBreweryRepository _dbRepository; // EfCore implementation when available
        private readonly IBreweryRepository _upstreamRepository; // Upstream-only repository
        private readonly BreweryDbContext? _dbContext;
        private readonly ILogger<BreweryOrchestrator> _logger;

        public BreweryOrchestrator(IBreweryRepository dbRepository, IBreweryRepository upstreamRepository, BreweryDbContext? dbContext, ILogger<BreweryOrchestrator> logger)
        {
            _dbRepository = dbRepository ?? throw new ArgumentNullException(nameof(dbRepository));
            _upstreamRepository = upstreamRepository ?? throw new ArgumentNullException(nameof(upstreamRepository));
            _dbContext = dbContext;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<IEnumerable<Brewery>> GetBreweriesAsync(BreweryQueryOptions options)
        {
            if (_dbContext != null)
            {
                var items = await _dbRepository.GetBreweriesAsync(options);
                if (items != null && items.Any()) return items;
                _logger.LogInformation("Local DB returned no results; falling back to upstream.");
                return await _upstreamRepository.GetBreweriesAsync(options);
            }

            return await _upstreamRepository.GetBreweriesAsync(options);
        }

        public async Task<IEnumerable<Brewery?>> GetBreweryByNameAsync(string name)
        {
            if (_dbContext != null)
            {
                var items = await _dbRepository.GetBreweryByNameAsync(name);
                if (items != null && items.Any()) return items;
                _logger.LogInformation("Local DB returned no results for name {Name}; falling back to upstream.", name);
                return await _upstreamRepository.GetBreweryByNameAsync(name);
            }

            return await _upstreamRepository.GetBreweryByNameAsync(name);
        }

        public async Task AddBreweryAsync(Brewery brewery)
        {
            if (_dbContext != null)
            {
                await _dbRepository.AddBreweryAsync(brewery);
                return;
            }

            await _upstreamRepository.AddBreweryAsync(brewery);
        }

        public async Task<BulkInsertResult> AddBreweriesAsync(IEnumerable<Brewery> breweries)
        {
            if (_dbContext != null)
            {
                return await _dbRepository.AddBreweriesAsync(breweries);
            }

            return await _upstreamRepository.AddBreweriesAsync(breweries);
        }

        public async Task<IEnumerable<Brewery>> SearchBreweriesAsync(string query)
        {
            if (_dbContext != null)
            {
                var items = await _dbRepository.SearchBreweriesAsync(query);
                if (items != null && items.Any()) return items;
                return await _upstreamRepository.SearchBreweriesAsync(query);
            }

            return await _upstreamRepository.SearchBreweriesAsync(query);
        }
    }
}
