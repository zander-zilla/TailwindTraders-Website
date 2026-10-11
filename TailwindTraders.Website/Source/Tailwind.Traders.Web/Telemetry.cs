using System;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Linq;
using Amazon;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Tailwind.Traders.Web
{
    /// <summary>App-specific spans and metrics (source/meter "TailwindTraders.Web").</summary>
    public static class AppTelemetry
    {
        public const string Name = "TailwindTraders.Web";
        public static readonly ActivitySource Source = new(Name, "1.0.0");
        private static readonly Meter Meter = new(Name, "1.0.0");

        /// <summary>Cart operations by op (get/add/update/delete) and result (ok/not_found/bad_request).</summary>
        public static readonly Counter<long> CartOperations =
            Meter.CreateCounter<long>("tailwindtraders.cart.operations", "{operation}", "Shopping cart operations");
        /// <summary>Units added to carts (sum of qty).</summary>
        public static readonly Counter<long> CartUnitsAdded =
            Meter.CreateCounter<long>("tailwindtraders.cart.units_added", "{unit}", "Product units added to carts");
        public static readonly Histogram<double> ImageSearchDuration =
            Meter.CreateHistogram<double>("tailwindtraders.image_search.duration", "s", "Image search latency (classify + catalog lookup)");
        public static readonly Counter<long> ImageSearchResults =
            Meter.CreateCounter<long>("tailwindtraders.image_search.requests", "{request}", "Image searches by result (match/no_match/error)");
    }

    /// <summary>
    /// OpenTelemetry wiring. Configured only through standard env vars:
    ///   OTEL_EXPORTER_OTLP_ENDPOINT / _PROTOCOL / _HEADERS (+ per-signal *_TRACES_/_METRICS_/_LOGS_ variants),
    ///   OTEL_SERVICE_NAME (default "tailwindtraders-web"), OTEL_RESOURCE_ATTRIBUTES,
    ///   OTEL_TRACES_SAMPLER(_ARG), OTEL_TRACES_EXPORTER / OTEL_METRICS_EXPORTER / OTEL_LOGS_EXPORTER = otlp|none.
    /// With no OTLP endpoint configured, instrumentation still runs (cheap) but nothing is exported.
    /// </summary>
    public static class TelemetryExtensions
    {
        public static WebApplicationBuilder AddObservability(this WebApplicationBuilder builder)
        {
            var cfg = builder.Configuration;
            bool hasEndpoint(string signal) =>
                !string.IsNullOrEmpty(cfg["OTEL_EXPORTER_OTLP_ENDPOINT"]) || !string.IsNullOrEmpty(cfg[$"OTEL_EXPORTER_OTLP_{signal}_ENDPOINT"]);
            bool export(string signal) =>
                hasEndpoint(signal) && !string.Equals(cfg[$"OTEL_{signal}_EXPORTER"], "none", StringComparison.OrdinalIgnoreCase);

            // Structured JSON logs to stdout (CloudWatch / Container Insights / kubectl logs), with trace + span ids.
            builder.Logging.ClearProviders();
            builder.Logging.Configure(o => o.ActivityTrackingOptions =
                ActivityTrackingOptions.TraceId | ActivityTrackingOptions.SpanId | ActivityTrackingOptions.ParentId);
            builder.Logging.AddJsonConsole(o =>
            {
                o.IncludeScopes = true; // TraceId/SpanId appear in "Scopes"
                o.UseUtcTimestamp = true;
                o.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ ";
            });

            var serviceName = cfg["OTEL_SERVICE_NAME"] ?? "tailwindtraders-web";
            var otel = builder.Services.AddOpenTelemetry()
                .ConfigureResource(r => r
                    .AddService(serviceName, serviceVersion: typeof(AppTelemetry).Assembly.GetName().Version?.ToString())
                    .AddEnvironmentVariableDetector()
                    .AddAttributes(new[] { new System.Collections.Generic.KeyValuePair<string, object>("deployment.environment.name", builder.Environment.EnvironmentName) }));

            otel.WithTracing(t =>
            {
                t.AddSource(AppTelemetry.Name)
                 .AddAspNetCoreInstrumentation(o =>
                 {
                     // Don't trace probes or static files.
                     o.Filter = ctx => !(ctx.Request.Path.StartsWithSegments("/healthz") || ctx.Request.Path.StartsWithSegments("/readyz")
                                         || ctx.Request.Path.StartsWithSegments("/static") || ctx.Request.Path.StartsWithSegments("/favicon.ico"));
                     o.RecordException = true;
                 })
                 .AddHttpClientInstrumentation(o => o.RecordException = true)
                 .AddNpgsql()                       // PostgreSQL commands (EF Core goes through Npgsql)
                 .AddAWSInstrumentation();          // AWS SDK calls (DynamoDB)
                if (export("TRACES")) t.AddOtlpExporter();
            });

            otel.WithMetrics(m =>
            {
                m.AddMeter(AppTelemetry.Name)
                 .AddAspNetCoreInstrumentation()
                 .AddHttpClientInstrumentation()
                 .AddRuntimeInstrumentation()
                 .AddMeter("Npgsql")
                 .AddAWSInstrumentation();
                if (export("METRICS")) m.AddOtlpExporter();
            });

            if (export("LOGS"))
            {
                builder.Logging.AddOpenTelemetry(l =>
                {
                    l.IncludeFormattedMessage = true;
                    l.IncludeScopes = true;
                    l.AddOtlpExporter();
                });
            }

            return builder;
        }
    }
}
