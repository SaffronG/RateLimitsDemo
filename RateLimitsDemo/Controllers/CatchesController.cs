using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace RateLimitsDemo.Controllers;

[ApiController]
[Route("[controller]")]
[AllowAnonymous]
public class CatchesController : ControllerBase
{
    // Stand-in for the Npgsql connection pool: 10 connections, 2-second wait timeout
    private static readonly SemaphoreSlim Pool = new(10, 10);

    [HttpPost]
    // [EnableRateLimiting("db-pool")]   // leave commented out for the "before" run
    public async Task<IActionResult> RecordCatch()
    {
        if (!await Pool.WaitAsync(TimeSpan.FromSeconds(2)))
            return Problem("Connection pool exhausted: timed out waiting for a connection",
                           statusCode: 500);
        try
        {
            await Task.Delay(1000);   // the "database work"
            return NoContent();
        }
        finally
        {
            Pool.Release();
        }
    }
}
