// ============================================================
// AppSettings.cs – NexaPay.Infrastructure/Settings
// ============================================================
// Konkret implementation av IAppSettings. Läser värden från
// appsettings.json (via IConfiguration) och exponerar dem som
// starkt typade properties. StaffDomain default "nexapay.com"
// om värdet saknas i config.
// ============================================================

using Microsoft.Extensions.Configuration;
using NexaPay.Application.Common.Interfaces;

namespace NexaPay.Infrastructure.Settings
{
    public class AppSettings : IAppSettings
    {
        public string StaffDomain { get; }

        public AppSettings(IConfiguration configuration)
        {
            StaffDomain = configuration["StaffDomain"] ?? "nexapay.com";
        }
    }
}
