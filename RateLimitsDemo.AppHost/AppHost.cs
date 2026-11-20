var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres")
                      .WithPgAdmin();             // optional: a web UI to browse the rows
var weatherDb = postgres.AddDatabase("weatherdb");

builder.AddDotnetProject("ratelimitsdemo", "../RateLimitsDemo/RateLimitsDemo.csproj")
       .WithOtlpExporter()
       .WithReference(weatherDb)                  // passes the connection string to the API
       .WaitFor(weatherDb);                       // API starts after Postgres is ready
       //.WithReplicas(2);   // Step 6: two copies of the API behind Aspire's load balancer

builder.Build().Run();
