using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using StartLine.Worker;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<Worker>();

// OpenTelemetry
var otlpEndpoint = builder.Configuration["Otel:Endpoint"] ?? "http://localhost:4317";
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService("StartLine.Worker"))
    .WithTracing(tracing => tracing
        .AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint)))
    .WithMetrics(metrics => metrics
        .AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint)))
    .WithLogging(
        logging => logging.AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint)),
        opts =>
        {
            opts.IncludeFormattedMessage = true;
            opts.IncludeScopes = true;
        });

var host = builder.Build();
host.Run();
