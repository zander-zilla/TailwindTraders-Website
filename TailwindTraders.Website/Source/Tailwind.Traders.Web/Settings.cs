using System.Text.Json.Serialization;

namespace Tailwind.Traders.Web
{
    public class Settings
    {
        public Settings()
        {
            LPKRouteHeader = string.Empty;
        }
        public string Auth { get; set; }
        public string ApiUrl { get; set; }
        public string ApiUrlShoppingCart { get; set; }
        public bool UseB2C { get; set; }

        public B2CAuth B2CAuth { get; set; }
        public ApplicationInsightsSettings ApplicationInsights { get; set; }
            = new ApplicationInsightsSettings();

        public DebugInformationSettings DebugInformation { get; set; }
            = new DebugInformationSettings();


        // This is set by the SettingsController, do not set via config because will be overriden

        public string LPKRouteHeader { get; private set; }

        public string ProductImagesUrl { get; set; }

        public void UselpkRouteHeader(string lpkRouteHeader) => LPKRouteHeader = lpkRouteHeader;

        public AcsSettings Acs { get; set; } = new AcsSettings();

        public string Email { get; set; }

        public string LogicAppUrl { get; set; }

        /// <summary>Browser (SPA) tracing. Public by design: only an OTLP/HTTP URL the browser can reach.</summary>
        public BrowserTelemetrySettings BrowserTelemetry { get; set; } = new BrowserTelemetrySettings();

        public Settings Clone()
        {
            var clone = new Settings()
            {
                ApiUrl = ApiUrl,
                Auth = Auth,
                ApiUrlShoppingCart = ApiUrlShoppingCart,
                UseB2C = UseB2C,
                BrowserTelemetry = BrowserTelemetry
            };

            return clone;
        }
    }

    public class BrowserTelemetrySettings
    {
        /// <summary>e.g. http://localhost:4318/v1/traces (Aspire dashboard). Empty = browser tracing off.</summary>
        public string OtlpTracesEndpoint { get; set; } = "";
        public string ServiceName { get; set; } = "tailwindtraders-spa";
    }

    public class B2CAuth
    {
        public string ClientId { get; set; }
        public string Authority { get; set; }
        public string Scopes { get; set; }
    }

    public class ApplicationInsightsSettings
    {
        public string InstrumentationKey { get; set; } = "";
    }

    public class DebugInformationSettings
    {
        public string SqlServerName { get; set; }
        public string CartTableName { get; set; }
        public string CustomText { get; set; }
        public bool ShowDebug { get; set; }
    }


    public class AcsSettings
    {
        public string ConnectionString { get; set; }
        public string Resource { get; set; }
    }
}