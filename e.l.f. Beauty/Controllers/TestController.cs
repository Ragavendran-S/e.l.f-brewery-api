using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace e.l.f._Beauty.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TestController : ControllerBase
    {
        [HttpGet("protected")]
        [Authorize]
        public IActionResult Protected()
        {
            return Ok(new { message = "authorized" });
        }
    }
}
