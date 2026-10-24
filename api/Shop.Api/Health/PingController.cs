using Microsoft.AspNetCore.Mvc;

namespace Shop.Api.Health;

// Keep-alive target for uptime monitors: stops Render's free tier sleeping without waking Neon,
// so it must never touch the database (unlike /api/health).
[ApiController]
[Route("api/ping")]
public sealed class PingController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok(new { status = "ok" });
}
