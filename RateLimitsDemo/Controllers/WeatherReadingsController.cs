using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace RateLimitsDemo.Controllers;

[ApiController]
[Route("weather")]
[AllowAnonymous]
public class WeatherReadingsController(WeatherDb db) : ControllerBase
{
    private static readonly string[] Summaries = ["Freezing", "Cool", "Mild", "Warm", "Hot"];

    [HttpPost]
    public async Task<IActionResult> Record()
    {
        // The transaction keeps one pooled connection checked out for the whole request
        await using var tx = await db.Database.BeginTransactionAsync();

        var reading = new WeatherReading
        {
            RecordedAt   = DateTime.UtcNow,
            TemperatureC = Random.Shared.Next(-20, 45),
            Summary      = Summaries[Random.Shared.Next(Summaries.Length)],
            ClientId     = Request.Headers["X-Client-Id"].FirstOrDefault()
        };
        db.Readings.Add(reading);
        await db.SaveChangesAsync();

        await db.Database.ExecuteSqlRawAsync("SELECT pg_sleep(1)");   // the "slow query"
        await tx.CommitAsync();

        return Created($"/weather/{reading.Id}", reading);
    }

    [HttpGet]
    public async Task<IActionResult> Latest() => Ok(new
    {
        total  = await db.Readings.CountAsync(),
        latest = await db.Readings.OrderByDescending(r => r.Id).Take(5).ToListAsync()
    });
}
