using System.IO;
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

builder.Services.AddControllers();
builder.Services.AddRazorPages();
builder.Services.Configure<Settings>(builder.Configuration);
builder.Services.AddStandalone(builder.Configuration, startupLogger);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Error");
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

app.MapControllers();
app.MapControllerRoute(name: "default", pattern: "{controller}/{action=Index}/{id?}");
app.MapRazorPages();

if (spaFiles != null)
{
    app.MapFallbackToFile("index.html", new StaticFileOptions { FileProvider = spaFiles });
}

app.Run();

// Exposed for WebApplicationFactory<Program> in tests.
public partial class Program { }
