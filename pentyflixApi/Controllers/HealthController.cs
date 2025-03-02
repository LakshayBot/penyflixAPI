using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace pentyflixApi.Controllers;

[ApiController]
[Route("[controller]")]
public class HealthController : ControllerBase
{
    [HttpGet("health")]
    public IActionResult HealthCheck()
    {
        try
        {
            return Ok(new { status = "success" });
        }
        catch
        {
            return StatusCode(500, new { status = "error" });
        }
    }
    [HttpGet("protected")]
    public IActionResult ProtectedData()
    {
        if (!User.Identity.IsAuthenticated)
        {
            return Unauthorized(new { message = "You are not authorized to access this resource" });
        }

        return Ok(new { message = "This is protected data!", user = User.Identity.Name });
    }
}
