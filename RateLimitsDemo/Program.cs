using Npgsql;
using RateLimitsDemo;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddNpgsqlDbContext<WeatherDb>("weatherdb", settings =>
{
    var cs = new NpgsqlConnectionStringBuilder(settings.ConnectionString)
    {
        MaxPoolSize = 10,   // FishDex uses the default of 100; small here so 40 requests can drain it
        Timeout = 2         // seconds to wait for a free connection before failing
    };
    settings.ConnectionString = cs.ConnectionString;
    settings.DisableRetry = true;   // fail visibly instead of quietly retrying
});
builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    await scope.ServiceProvider.GetRequiredService<WeatherDb>().Database.EnsureCreatedAsync();
}

app.MapDefaultEndpoints();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// Stamp every response with the process id (used in Step 6)
app.Use(async (ctx, next) =>
{
    ctx.Response.Headers["X-Instance"] = Environment.ProcessId.ToString();
    await next();
});

app.MapControllers();

app.Run();
