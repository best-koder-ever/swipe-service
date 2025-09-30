using Microsoft.AspNetCore.Mvc;

namespace SwipeService.Controllers;

[ApiController]
[Route("health")]
public class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok(new { status = "Healthy", service = "SwipeService", timestamp = System.DateTime.UtcNow });
}
