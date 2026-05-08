using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NexaPay.API;
using NexaPay.Infrastructure.Persistence;
using System.Threading.RateLimiting;

namespace NexaPay.Tests.Integration
{
    public class NexaPayWebApplicationFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");

            builder.ConfigureServices(services =>
            {
                // Ta bort SQL Server DbContext-registreringen
                var descriptor = services.SingleOrDefault(d =>
                    d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));
                if (descriptor != null)
                    services.Remove(descriptor);

                // Ersätt med InMemory – unik databas per factory-instans
                // Guid utvärderas EN GÅNG här, inte vid varje DbContext-instansiering
                var dbName = $"NexaPayTest-{Guid.NewGuid()}";
                services.AddDbContext<ApplicationDbContext>(options =>
                    options.UseInMemoryDatabase(dbName));

                // Ta bort befintlig rate limiter-konfiguration och ersätt med obegränsad
                var rateLimiterConfigType = typeof(IConfigureOptions<RateLimiterOptions>);
                var rateLimiterDescriptors = services
                    .Where(d => d.ServiceType == rateLimiterConfigType)
                    .ToList();
                foreach (var d in rateLimiterDescriptors)
                    services.Remove(d);

                services.Configure<RateLimiterOptions>(options =>
                    options.AddPolicy("auth", _ =>
                        RateLimitPartition.GetNoLimiter("test")));
            });
        }
    }
}
