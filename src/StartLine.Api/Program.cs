using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using StartLine.Api;
using StartLine.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Infrastructure (DB, repositories, auth services, JWT)
builder.Services.AddInfrastructure(builder.Configuration);

// Health checks
builder.Services.AddHealthChecks()
    .AddNpgSql(
        builder.Configuration.GetConnectionString("DefaultConnection")!,
        name: "postgresql",
        tags: new[] { "ready" });

// OpenTelemetry
var otlpEndpoint = builder.Configuration["Otel:Endpoint"] ?? "http://localhost:4317";
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService("StartLine.Api"))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint)))
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint)))
    .WithLogging(
        logging => logging.AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint)),
        opts =>
        {
            opts.IncludeFormattedMessage = true;
            opts.IncludeScopes = true;
        });

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

// Controllers
builder.Services.AddControllers()
    .AddJsonOptions(o =>
        o.JsonSerializerOptions.Converters.Add(
            new System.Text.Json.Serialization.JsonStringEnumConverter()));

// Rate limiting: sliding window per IP on auth endpoints
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("auth", context =>
    {
        var ipAddress = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetSlidingWindowLimiter(ipAddress, _ =>
            new SlidingWindowRateLimiterOptions
            {
                PermitLimit = builder.Configuration.GetValue("RateLimit:AuthPermitLimit", 5),
                Window = TimeSpan.FromSeconds(builder.Configuration.GetValue("RateLimit:AuthWindowSeconds", 60)),
                SegmentsPerWindow = 2,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            });
    });

    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

// Health endpoints
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false  // No checks needed for liveness
});

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});

app.MapControllers();

app.Run();

public partial class Program { }

