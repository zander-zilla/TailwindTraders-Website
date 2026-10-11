namespace Tailwind.Traders.Web
{
    /// <summary>
    /// Public, browser-facing settings served by /api/settings. Never put secrets here.
    /// Phase 7 removed AAD B2C, Application Insights, Azure Communication Services (incl. its connection string,
    /// which was served to the browser), Logic App/Teams meeting and Dev Spaces settings.
    /// </summary>
    public class Settings
    {
        public string Auth { get; set; }
        public string ApiUrl { get; set; }
        public string ApiUrlShoppingCart { get; set; }

        public DebugInformationSettings DebugInformation { get; set; } = new DebugInformationSettings();

        public string ProductImagesUrl { get; set; }

        /// <summary>Browser (SPA) tracing. Public by design: only an OTLP/HTTP URL the browser can reach.</summary>
        public BrowserTelemetrySettings BrowserTelemetry { get; set; } = new BrowserTelemetrySettings();
    }

    public class BrowserTelemetrySettings
    {
        /// <summary>e.g. http://localhost:4318/v1/traces (Aspire dashboard). Empty = browser tracing off.</summary>
        public string OtlpTracesEndpoint { get; set; } = "";
        public string ServiceName { get; set; } = "tailwindtraders-spa";
    }

    public class DebugInformationSettings
    {
        public string SqlServerName { get; set; }
        public string CartTableName { get; set; }
        public string CustomText { get; set; }
        public bool ShowDebug { get; set; }
    }
}
