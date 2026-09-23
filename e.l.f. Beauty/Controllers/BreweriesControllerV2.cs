using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using e.l.f._Beauty.Models;
using e.l.f._Beauty.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

// For more information on enabling MVC for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace e.l.f._Beauty.Controllers
{
    [ApiController]
    [ApiVersion("2.0")]
    [Route("api/v{version:apiVersion}/breweries")]
    [ApiExplorerSettings(GroupName = "v2")]
    [Authorize]
    public class BreweriesControllerV2 : ControllerBase
    {
        private readonly IBreweryService _service;
        private readonly ILogger<BreweryService> _logger;
        public BreweriesControllerV2(IBreweryService service, ILogger<BreweryService> logger) { _service = service; _logger = logger; }

        [HttpGet]
      
        public async Task<IActionResult> GetBreweries([FromQuery] BreweryQueryOptions options)
        {
            var result = await _service.GetBreweriesAsync(options);
            return Ok(result);
        }
    }
}

