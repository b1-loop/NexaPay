// ============================================================
// DependencyInjection.cs – NexaPay.Infrastructure
// ============================================================
// Samlar alla DI-registreringar för Infrastructure-lagret.
// Anropas från Program.cs med:
//   builder.Services.AddInfrastructure(builder.Configuration)
//
// Registrerar:
//   - Entity Framework Core med SQL Server
//   - UnitOfWork och alla Repositories
//   - JwtService
//   - JWT-autentisering
// ============================================================

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using NexaPay.Domain.Interfaces;
using NexaPay.Infrastructure.Identity;
using NexaPay.Infrastructure.Persistence;
using NexaPay.Infrastructure.Persistence.Repositories;
using System.Text;

namespace NexaPay.Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(
            this IServiceCollection services,
            IConfiguration configuration) // IConfiguration för connection string och JWT
        {
            // --------------------------------------------------------
            // Entity Framework Core – SQL Server
            // --------------------------------------------------------
            services.AddDbContext<ApplicationDbContext>(options =>
            {
                options.UseSqlServer(
                    // Hämta connection string från appsettings.json
                    // "DefaultConnection" är nyckeln i ConnectionStrings-sektionen
                    configuration.GetConnectionString("DefaultConnection"),

                    // sqlOptions låter oss konfigurera SQL Server-specifika inställningar
                    sqlOptions =>
                    {
                        // Aktivera automatiska retries vid tillfälliga fel
                        // T.ex. om SQL Server är tillfälligt otillgänglig
                        // Försöker upp till 3 gånger med exponentiell backoff
                        sqlOptions.EnableRetryOnFailure(
                            maxRetryCount: 3,
                            maxRetryDelay: TimeSpan.FromSeconds(30),
                            errorNumbersToAdd: null);
                    });
            });

            // --------------------------------------------------------
            // Unit of Work och Repositories
            // --------------------------------------------------------
            // Scoped = en ny instans per HTTP-request
            // Det är viktigt att UnitOfWork är Scoped eftersom
            // DbContext är Scoped – de måste ha samma livslängd

            // Registrera UnitOfWork – används av alla handlers
            services.AddScoped<IUnitOfWork, UnitOfWork>();

            // --------------------------------------------------------
            // JWT Service
            // --------------------------------------------------------
            // Scoped – en ny instans per request
            services.AddScoped<IJwtService, JwtService>();

            // --------------------------------------------------------
            // JWT-autentisering
            // --------------------------------------------------------
            // Konfigurerar ASP.NET Core att validera JWT-tokens
            // på inkommande requests
            var jwtKey = configuration["Jwt:Key"]
                ?? throw new InvalidOperationException(
                    "JWT-nyckeln saknas i konfigurationen");

            services.AddAuthentication(options =>
            {
                // Sätt JWT Bearer som standard autentiseringsschema
                // Det betyder att alla [Authorize]-attribut använder JWT
                options.DefaultAuthenticateScheme =
                    JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme =
                    JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                // TokenValidationParameters definierar hur tokens valideras
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    // Validera att token utfärdades av rätt issuer
                    ValidateIssuer = true,
                    ValidIssuer = configuration["Jwt:Issuer"],

                    // Validera att token är avsedd för rätt audience
                    ValidateAudience = true,
                    ValidAudience = configuration["Jwt:Audience"],

                    // Validera att token inte har gått ut
                    ValidateLifetime = true,

                    // Validera signaturen med vår hemliga nyckel
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(jwtKey)),

                    // Tillåt inte tokens som gått ut
                    // ClockSkew = hur mycket tidsskillnad vi tolererar
                    // mellan server och klient – sätts till 0 för strikthet
                    ClockSkew = TimeSpan.Zero
                };
            });

            // Lägg till Authorization-tjänster
            // Krävs för [Authorize(Roles = "Admin")] att fungera
            services.AddAuthorization();

            return services;
        }
    }
}