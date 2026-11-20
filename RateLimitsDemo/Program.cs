using Microsoft.AspNetCore.RateLimiting;
using Npgsql;
using RateLimitsDemo;
using System.Threading.RateLimiting;

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

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // Tell the client how long to wait before retrying
    options.OnRejected = async (context, ct) =>
    {
        RateLimitTelemetry.Record(context.HttpContext, "rejected");

        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
            context.HttpContext.Response.Headers.RetryAfter =
                ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString();

        await context.HttpContext.Response.WriteAsJsonAsync(
            new { error = "Too many requests. Slow down." }, ct);
    };

    // One token bucket PER CLIENT: burst of 5, then 1 new request every 2 seconds
    options.AddPolicy("per-client", http =>
    {
        // No login needed: the client names itself with a header, else we use its IP
        var clientId = RateLimitTelemetry.GetClientId(http);

        return RateLimitPartition.GetTokenBucketLimiter(clientId, _ => new TokenBucketRateLimiterOptions
        {
            TokenLimit = 5,
            TokensPerPeriod = 1,
            ReplenishmentPeriod = TimeSpan.FromSeconds(2),
            QueueLimit = 0,              // reject immediately instead of waiting
            AutoReplenishment = true
        });
    });

    // Cap in-flight database work BELOW the pool size, with a short waiting line
    options.AddConcurrencyLimiter("db-pool", o =>
    {
        o.PermitLimit = 8;    // pool has 10; leave headroom
        o.QueueLimit = 4;     // a short line, not an unbounded pile-up
        o.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
    });
});

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

// stamp every response with the process id (used in Step 6)
app.Use(async (ctx, next) =>
{
    ctx.Response.Headers["X-Instance"] = Environment.ProcessId.ToString();
    await next();
});

// must be before MapControllers() so that the rate limiting middleware runs before the controller action
app.UseRateLimiter();

// Runs only for requests the limiter let through (rejected ones short-circuit above)
app.Use(async (ctx, next) =>
{
    if (ctx.GetEndpoint()?.Metadata.GetMetadata<EnableRateLimitingAttribute>() is not null)
        RateLimitTelemetry.Record(ctx, "allowed");
    await next();
});

app.MapControllers();

app.Run();