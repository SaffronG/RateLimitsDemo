using Microsoft.EntityFrameworkCore;

namespace RateLimitsDemo;

public class WeatherReading
{
    public int Id { get; set; }
    public DateTime RecordedAt { get; set; }
    public int TemperatureC { get; set; }
    public string? Summary { get; set; }
    public string? ClientId { get; set; }
}

public class WeatherDb(DbContextOptions<WeatherDb> options) : DbContext(options)
{
    public DbSet<WeatherReading> Readings => Set<WeatherReading>();
}
