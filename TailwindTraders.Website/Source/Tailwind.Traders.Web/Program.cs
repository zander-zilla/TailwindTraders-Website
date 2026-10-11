using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Tailwind.Traders.Web;
using Tailwind.Traders.Web.Standalone;

var builder = WebApplication.CreateBuilder(args);

// Logger for startup-time work (Standalone DB seeding) before the app is built.
using var startupLoggerFactory = LoggerFactory.Create(l => l.AddConsole());
var startupLogger = startupLoggerFactory.CreateLogger("Startup");

builder.AddObservability(); // OpenTelemetry (OTEL_* env vars) + JSON logs with trace ids
builder.Services.AddControllers();
builder.Services.AddProblemDetails();
// Liveness (/healthz) = process only. Readiness (/readyz) adds PostgreSQL + DynamoDB checks (tag "ready").
var health = builder.Services.AddHealthChecks();
if (builder.Configuration.IsStandaloneEnabled())
{
    health.AddCheck<Tailwind.Traders.Web.Standalone.CatalogDbReadinessCheck>("postgres", tags: new[] { "ready" }, timeout: TimeSpan.FromSeconds(3));
    health.AddCheck<Tailwind.Traders.Web.Standalone.CartTableReadinessCheck>("dynamodb", tags: new[] { "ready" }, timeout: TimeSpan.FromSeconds(3));
}
builder.Services.Configure<Settings>(builder.Configuration);
builder.Services.AddStandalone(builder.Configuration, startupLogger);

var app = builder.Build();

// Separate migrate step (Kubernetes Job / compose "migrate" service): apply EF Core migrations + seed, then exit.
if (args.Contains("--migrate"))
{
    if (!builder.Configuration.IsStandaloneEnabled())
    {
        startupLogger.LogError("--migrate requires ConnectionStrings:Products");
        return 1;
    }
    await ConfigExtensions.MigrateAndSeedAsync(app.Services, startupLogger);
    return 0;
}
if (builder.Configuration.IsStandaloneEnabled() && builder.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
{
    await ConfigExtensions.MigrateAndSeedAsync(app.Services, startupLogger); // local dev / tests convenience
}

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler(); // ProblemDetails (Razor Error page removed)
}

// React SPA: served as static files from ClientApp/build (built by `npm run build` / the Dockerfile).
// In development run `npm start` in ClientApp separately (CRA dev server proxies /api to this app).
var spaRoot = Path.Combine(app.Environment.ContentRootPath, "ClientApp", "build");
PhysicalFileProvider spaFiles = Directory.Exists(spaRoot) ? new PhysicalFileProvider(spaRoot) : null;

app.UseStaticFiles();
if (spaFiles != null)
{
    app.UseStaticFiles(new StaticFileOptions { FileProvider = spaFiles, ServeUnknownFileTypes = true });
}

app.UseStandalone(builder.Configuration, startupLogger);
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/healthz", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions { Predicate = _ => false }); // liveness: process up
app.MapHealthChecks("/readyz", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions { Predicate = c => c.Tags.Contains("ready") }); // readiness: dependencies
app.MapControllers();
app.MapControllerRoute(name: "default", pattern: "{controller}/{action=Index}/{id?}");

if (spaFiles != null)
{
    app.MapFallbackToFile("index.html", new StaticFileOptions { FileProvider = spaFiles });
}

await app.RunAsync();
return 0;

// Exposed for WebApplicationFactory<Program> in tests.
public partial class Program { }
