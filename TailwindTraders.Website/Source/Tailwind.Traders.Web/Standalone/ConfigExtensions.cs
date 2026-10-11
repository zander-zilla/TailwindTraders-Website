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
using Amazon;
using Amazon.DynamoDBv2;
using Amazon.Runtime;
using Tailwind.Traders.Web.Standalone.Cart;
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

            // Cart: Amazon DynamoDB (Phase 3b, ADR 0008). Credentials: AWS SDK default chain
            // (env vars, profile, EKS Pod Identity / IRSA). With an endpoint override (DynamoDB Local) dummy keys are used.
            services.Configure<CartOptions>(config.GetSection("Cart"));
            services.AddSingleton<IAmazonDynamoDB>(_ => CreateDynamoDbClient(config.GetSection("Cart").Get<CartOptions>() ?? new CartOptions()));
            services.AddSingleton<ICartRepository, DynamoDbCartRepository>();

            services.AddTransient<IProductService, ProductService>();
            
            var useCustomVisionApi = !string.IsNullOrEmpty(config["ImagePredictorEndpoint"]);
            if (useCustomVisionApi)
            {
                services.AddHttpClient(HttpEndpointSearchTermPredictor.HttpClientName)
                    .AddStandardResilienceHandler(); // Microsoft.Extensions.Http.Resilience (Polly v8)
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
            await new CartTableInitializer(
                scope.ServiceProvider.GetRequiredService<IAmazonDynamoDB>(),
                scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<CartOptions>>(),
                logger).EnsureTableAsync();
        }

        public static IAmazonDynamoDB CreateDynamoDbClient(CartOptions o)
        {
            var cfg = new AmazonDynamoDBConfig();
            var region = string.IsNullOrEmpty(o.Region) ? Environment.GetEnvironmentVariable("AWS_REGION") : o.Region;
            if (!string.IsNullOrEmpty(o.ServiceUrl))
            {
                cfg.ServiceURL = o.ServiceUrl;                                   // DynamoDB Local / LocalStack
                cfg.AuthenticationRegion = string.IsNullOrEmpty(region) ? "us-east-1" : region;
                var hasKeys = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AWS_ACCESS_KEY_ID"));
                return hasKeys ? new AmazonDynamoDBClient(cfg) : new AmazonDynamoDBClient(new BasicAWSCredentials("local", "local"), cfg);
            }
            cfg.RegionEndpoint = RegionEndpoint.GetBySystemName(string.IsNullOrEmpty(region) ? "us-east-1" : region);
            return new AmazonDynamoDBClient(cfg);                                // default credential chain
        }
    }
}