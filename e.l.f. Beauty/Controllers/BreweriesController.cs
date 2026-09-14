using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using e.l.f._Beauty.Models;
using e.l.f._Beauty.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

// For more information on enabling MVC for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace e.l.f._Beauty.Controllers
{
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/Breweries")]
    [ApiExplorerSettings(GroupName = "v1")]
    //[Authorize]
    public class BreweriesController : ControllerBase
    {
        private readonly IBreweryService _service;
        private readonly ILogger<BreweryService> _logger;
        private readonly IBreweryRepository _repository;

        public BreweriesController(IBreweryService service, ILogger<BreweryService> logger,IBreweryRepository repository) { _service = service; _logger = logger;_repository = repository; }

        [HttpGet]
        //[Authorize]
        public async Task<ActionResult<PagedResult<Brewery>>> GetBreweries([FromQuery] BreweryQueryOptions options)
        {
            var result = await _service.GetBreweriesAsync(options);
            return Ok(result);
        }
        
        [HttpGet("autocomplete")]
        public async Task<IActionResult> Autocomplete([FromQuery] string query)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(query))
                    return BadRequest("Query cannot be empty.");

                var suggestions = await _service.SearchBreweriesAsync(query);
                if (!suggestions.Any())
                    return NotFound("No breweries found.");
                return Ok(suggestions);

            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex, "Error fetching breweries from external API.");
                return StatusCode(503, "External API unavailable.");

            }
            
        }
        [HttpGet("{Name}")]
        public async Task<IActionResult> GetBrewery(string name)
        {
            var brewery = await _repository.GetBreweryByNameAsync(name);
            if (brewery == null) return NotFound();
            return Ok(brewery);
        }

        [HttpPost]
        public async Task<IActionResult> AddBrewery(Brewery brewery)
        {
            await _repository.AddBreweryAsync(brewery);
            return CreatedAtAction(nameof(GetBrewery), new { id = brewery.Id }, brewery);
        }

    }



}

