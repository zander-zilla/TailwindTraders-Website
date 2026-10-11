using System;
using Microsoft.EntityFrameworkCore;
using System.IO;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver;
using Tailwind.Traders.Web.Standalone.Data;
using Tailwind.Traders.Web.Standalone.Services;

namespace Tailwind.Traders.Web.Standalone
{
    public static class ConfigExtensions
    {
        public static void AddStandalone(this IServiceCollection services, IConfiguration config, ILogger logger)
        {
            if (!IsStandaloneEnabled(config))
            {
                return;
            }

            services.AddDbContextPool<CatalogDbContext>(o => CatalogDbContext.Configure(o, GetCatalogConnectionString(config)));

            // appsettings.json ships MongoConnectionString = "" so `??` never applied (legacy bug); treat empty as unset.
            var mongoConnectionString = string.IsNullOrEmpty(config["MongoConnectionString"]) ? "mongodb://localhost:27017" : config["MongoConnectionString"];
            var mongoClientSettings = MongoClientSettings.FromConnectionString(mongoConnectionString);
            // App Insights dependency tracking for Mongo removed (Azure-specific); OpenTelemetry arrives in Phase 6.
            var mongoClient = new MongoClient(mongoClientSettings);
            services.AddSingleton(mongoClient);

            services.AddTransient<IProductService, ProductService>();
            
            var useCustomVisionApi = !string.IsNullOrEmpty(config["ImagePredictorEndpoint"]);
            if (useCustomVisionApi)
            {
                services.AddSingleton<IImageSearchTermPredictor, HttpEndpointSearchTermPredictor>();
            }
            else
            {
                services.AddSingleton<IImageSearchTermPredictor, OnnxImageSearchTermPredictor>();
            }
            
            services.AddTransient<IImageSearchService, ImageSearchService>();

            // demo only, do not do this in real life!
            const string defaultSecurityKey = Constants.DefaultJwtSigningKey;
            var securityKey = config["SecurityKey"] ?? defaultSecurityKey;
            var key = Encoding.ASCII.GetBytes(securityKey);
            services.AddAuthentication(x =>
            {
                x.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                x.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(x =>
            {
                x.RequireHttpsMetadata = false;
                x.SaveToken = true;
                x.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = false,
                    ValidateAudience = false
                };
            });

        }

        public static void UseStandalone(this IApplicationBuilder app, IConfiguration config, ILogger logger)
        {
            if (!IsStandaloneEnabled(config))
            {
                return;
            }

            var ProductImagesPath = Path.Combine(Directory.GetCurrentDirectory(), "app_data", "productimages");
            if (Directory.Exists(ProductImagesPath))
            {
                app.UseStaticFiles(new StaticFileOptions
                {
                    RequestPath = "/productimages",
                    FileProvider = new PhysicalFileProvider(ProductImagesPath)
                });
            }
        }

        public static bool IsStandaloneEnabled(this IConfiguration config)
        {
            return !string.IsNullOrEmpty(GetCatalogConnectionString(config));
        }

        /// <summary>PostgreSQL connection string for the catalog: ConnectionStrings:Products (env ConnectionStrings__Products).</summary>
        public static string GetCatalogConnectionString(IConfiguration config) => config.GetConnectionString("Products");

        /// <summary>Apply migrations + idempotent seed. Used by `--migrate` and Database:MigrateOnStartup.</summary>
        public static async System.Threading.Tasks.Task MigrateAndSeedAsync(IServiceProvider services, ILogger logger)
        {
            using var scope = services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
            await new StandaloneDatabaseInitializer(db, logger).MigrateAndSeedAsync();
        }
    }
}