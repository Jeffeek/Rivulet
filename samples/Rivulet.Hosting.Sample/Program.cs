using Rivulet.Diagnostics;
using Rivulet.Hosting;
using Rivulet.Hosting.Sample;

// ReSharper disable ArrangeObjectCreationWhenTypeNotEvident

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();

// Configure Rivulet from appsettings.json
builder.Services.AddRivulet(builder.Configuration);

// Add background workers
builder.Services.AddHostedService<DataProcessingWorker>();
builder.Services.AddHostedService<QueueProcessingWorker>();

// Add Rivulet metrics exporter
builder.Services.AddSingleton<PrometheusExporter>();

// Add health checks for Rivulet operations
builder.Services.AddHealthChecks()
    .AddCheck<RivuletHealthCheck>("rivulet", tags: ["ready", "live"]);

var app = builder.Build();

app.UseHttpsRedirection();
app.UseAuthorization();

// Map health check endpoints
app.MapHealthChecks("/health/ready",
    new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
    {
        Predicate = static check => check.Tags.Contains("ready")
    });

app.MapHealthChecks("/health/live",
    new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
    {
        Predicate = static check => check.Tags.Contains("live")
    });

// Map Prometheus metrics endpoint
app.MapGet("/metrics",
    static (PrometheusExporter exporter) =>
    {
        var metrics = exporter.Export();
        return Results.Text(metrics, "text/plain; version=0.0.4");
    });

// Map controllers
app.MapControllers();

// Root endpoint with information
app.MapGet("/",
    static () => Results.Ok(new
    {
        application = "Rivulet.Hosting.Sample",
        version = "1.3.0",
        endpoints = new
        {
            healthReady = "/health/ready",
            healthLive = "/health/live",
            metrics = "/metrics",
            api = new
            {
                square = "POST /api/batch/square",
                fetch = "POST /api/batch/fetch",
                batchSum = "POST /api/batch/batch-sum"
            }
        },
        configuration = new
        {
            message = "Rivulet options are loaded from appsettings.json",
            section = "Rivulet"
        }
    }));

Console.WriteLine("=== Rivulet.Hosting Sample ===");
Console.WriteLine("Starting ASP.NET Core application with Rivulet integration");
Console.WriteLine();
Console.WriteLine("Features:");
Console.WriteLine("  - Background workers using ParallelWorkerService and ParallelBackgroundService");
Console.WriteLine("  - Configuration binding from appsettings.json");
Console.WriteLine("  - Dependency injection for ParallelOptionsRivulet");
Console.WriteLine("  - Health checks for Rivulet operations");
Console.WriteLine("  - Prometheus metrics endpoint");
Console.WriteLine("  - API endpoints using Rivulet for parallel processing");
Console.WriteLine();
Console.WriteLine("Available endpoints:");
Console.WriteLine("  - GET  /           - Application information");
Console.WriteLine("  - GET  /health/ready  - Readiness health check");
Console.WriteLine("  - GET  /health/live   - Liveness health check");
Console.WriteLine("  - GET  /metrics       - Prometheus metrics");
Console.WriteLine("  - POST /api/batch/square      - Square numbers");
Console.WriteLine("  - POST /api/batch/fetch       - Fetch URLs");
Console.WriteLine("  - POST /api/batch/batch-sum   - Sum in batches");
Console.WriteLine();

app.Run();
