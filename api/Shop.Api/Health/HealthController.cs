using Microsoft.AspNetCore.Mvc;
using Shop.Infrastructure.Data;

namespace Shop.Api.Health;

[ApiController]
[Route("api/health")]
public sealed class HealthController(ShopDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var database = await db.Database.CanConnectAsync(ct);
        return database
            ? Ok(new { status = "ok" })
            : StatusCode(StatusCodes.Status503ServiceUnavailable, new { status = "degraded" });
    }
}
