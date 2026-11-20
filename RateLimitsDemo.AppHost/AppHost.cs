var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.RateLimitsDemo>("ratelimitsdemo");

builder.Build().Run();
