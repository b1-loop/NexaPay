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
