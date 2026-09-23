using e.l.f._Beauty.Models;
using e.l.f._Beauty.Repository;
using e.l.f._Beauty.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

// For more information on enabling MVC for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace e.l.f._Beauty.Controllers
{
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/breweries")]
    [ApiExplorerSettings(GroupName = "v1")]
    [Authorize]
    public class BreweriesController : ControllerBase
    {
        private readonly IBreweryService _service;
        private readonly ILogger<BreweryService> _logger;
        private readonly IBreweryRepository _repository;
        private readonly IPagingHelper _pagingHelper;
        private readonly BreweryDbContext? _dbContext;
        [ActivatorUtilitiesConstructor]
        public BreweriesController(IBreweryService service, ILogger<BreweryService> logger, IBreweryRepository repository, BreweryDbContext? dbContext, IPagingHelper pagingHelper)
        {
            _service = service;
            _logger = logger;
            _repository = repository;
            // dbContext may be null when no local DB is configured; repository handles null DbContext.
            _dbContext = dbContext;
            _pagingHelper = pagingHelper ?? throw new ArgumentNullException(nameof(pagingHelper));
        }

        // Convenience ctor for unit tests that only need the service and a logger
        public BreweriesController(IBreweryService service, ILogger<BreweryService> logger)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _repository = null!;
            _dbContext = null!;
            _pagingHelper = null!;
        }

        [HttpGet]
        public async Task<ActionResult<PagedResult<Brewery>>> GetBreweries([FromQuery] BreweryQueryOptions options)
        {
            // Validate client-provided combinations to return a 400 Bad Request for
            // client-correctable mistakes (e.g. requesting distance sort without
            // providing user coordinates). This prevents such errors from bubbling
            // into unhandled exceptions and ensures consistent client-facing
            // responses.
            if (!string.IsNullOrWhiteSpace(options?.SortBy) && options.SortBy.Equals("distance", StringComparison.OrdinalIgnoreCase))
            {
                if (!options.UserLat.HasValue || !options.UserLng.HasValue)
                {
                    var pd = new ProblemDetails
                    {
                        Title = "Bad request",
                        Detail = "User latitude and longitude are required when sorting by distance.",
                        Status = StatusCodes.Status400BadRequest
                    };
                    pd.Extensions["paramName"] = "sortBy";
                    return BadRequest(pd);
                }
            }

            var result = await _service.GetBreweriesAsync(options);
            return Ok(result);
        }

        // Duplicate DbContext-backed endpoint removed. All listing endpoints must use the service pipeline
        // (GetBreweries([FromQuery] BreweryQueryOptions)) to ensure consistent caching, filtering and sorting.

        [HttpGet("autocomplete")]
        public async Task<IActionResult> Autocomplete([FromQuery] string query)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(query))
                    return BadRequest("Query cannot be empty.");

                var results = await _service.AutocompleteAsync(query);
                if (results == null)
                    return NotFound(new { message = "No autocomplete results found for the provided query." });

                // Return empty list as 200 OK so clients receive a consistent collection type
                if (!results.Any())
                    return Ok(results);

                return Ok(results);

            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex, "Error fetching breweries from external API.");
                return StatusCode(StatusCodes.Status503ServiceUnavailable, "External API unavailable.");
            }

        }
        [HttpGet("{name}")]
        public async Task<IActionResult> GetBrewery(string name)
        {
            // Normalize common URL-friendly forms (e.g. hyphens) into the
            // upstream API's expected value. Many clients use hyphens in place
            // of spaces when constructing URLs (e.g. 'Sierra-Nevada'), but the
            // upstream 'by_name' filter expects the real name (spaces).
            var normalized = (name ?? string.Empty).Replace('-', ' ');

            // The repository returns a collection from the upstream API. Return the
            // first matching brewery (if any) so this endpoint matches the single-item
            // resource semantics of /breweries/{name}.
            var breweries = await _repository.GetBreweryByNameAsync(normalized);
            var brewery = breweries?.FirstOrDefault();
            if (brewery == null) return NotFound();
            return Ok(brewery);
        }

        [HttpPost]
        public async Task<IActionResult> AddBrewery(Brewery brewery)
        {
            await _repository.AddBreweryAsync(brewery);
            // Return location using the route parameter name expected by GetBrewery
            return CreatedAtAction(nameof(GetBrewery), new { name = brewery.Name }, brewery);
        }

    }



}

