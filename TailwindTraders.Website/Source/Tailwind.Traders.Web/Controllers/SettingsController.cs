using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using System.Text.RegularExpressions;

namespace Tailwind.Traders.Web.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SettingsController : ControllerBase
    {
        private readonly Settings _settings;
        public SettingsController(IOptionsSnapshot<Settings> settings, IConfiguration config)
        {
            _settings = settings.Value;
            var catalogConnection = config.GetConnectionString("Products");
            if (!string.IsNullOrEmpty(catalogConnection))
            {
                // Debug header shows the catalog DB host (PostgreSQL since Phase 3a). Property name kept for the SPA.
                var match = Regex.Match(catalogConnection, @"(?i)(Host|Server)=(?<servername>[^,;]+)");
                _settings.DebugInformation.SqlServerName = match.Groups["servername"].Value;
            }
            _settings.DebugInformation.CartTableName = config["Cart:TableName"] ?? "tt-cart"; // DynamoDB cart (Phase 3b)
        }

        [HttpGet()]
        public ActionResult<Settings> GetSettings() => Ok(_settings);

     }
}
