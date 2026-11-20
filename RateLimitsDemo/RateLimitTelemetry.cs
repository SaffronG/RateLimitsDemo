using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace RateLimitsDemo;

public static class RateLimitTelemetry
{
    public const string MeterName = "RateLimitsDemo.RateLimitTelemetry";
    private static readonly Meter Meter = new(MeterName);

    private static readonly Counter<long> Decisions = Meter.CreateCounter<long>("ratelimit.decisions", unit: "{request}", description: "Rate limiter decisions by client and outcome");
    public static string GetClientId(HttpContext http) => http.Request.Headers["X-Client-Id"].FirstOrDefault()
        ?? http.Connection.RemoteIpAddress?.ToString()
        ?? "unknown";

    public static void Record(HttpContext http, string outcome)
    {
        var clientId = GetClientId(http);
        Decisions.Add(1, new KeyValuePair<string, object?>("client.id", clientId), new KeyValuePair<string, object?>("outcome", outcome));

        var activity = Activity.Current;
        activity?.SetTag("ratelimit.client_id", clientId);
        activity?.SetTag("ratelimit.outcome", outcome);
        if (outcome == "rejected")
            activity?.AddEvent(new ActivityEvent("rate_limited"));
    }
}
