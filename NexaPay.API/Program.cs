// ============================================================
// Program.cs – NexaPay.API
// ============================================================
// Startpunkten för NexaPay API.
// Ren och minimal – allt ansvar är utbrutit till
// ServiceExtensions.cs och DatabaseExtensions.cs
// ============================================================

using NexaPay.Application;
using NexaPay.Infrastructure;
using NexaPay.API;

namespace NexaPay.API
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            // ============================================================
            // Bygg applikationen
            // ============================================================
            var builder = WebApplication.CreateBuilder(args);

            // --------------------------------------------------------
            // Registrera tjänster – varje rad har ett tydligt ansvar
            // --------------------------------------------------------

            // Application-lagret: MediatR, AutoMapper, FluentValidation
            builder.Services.AddApplication();

            // Infrastructure-lagret: EF Core, Repositories, JWT
            builder.Services.AddInfrastructure(builder.Configuration);

            // Identity: Användare, lösenord och roller
            builder.Services.AddIdentityServices();

            // API: Controllers, Swagger, CORS
            builder.Services.AddApiServices();

            // ============================================================
            // Bygg applikationen
            // ============================================================
            var app = builder.Build();

            // --------------------------------------------------------
            // Initalisera databas – migrationer och seed-data
            // --------------------------------------------------------
            await app.InitialiseDatabaseAsync();

            // --------------------------------------------------------
            // Konfigurera middleware-pipeline
            // --------------------------------------------------------
            app.UseApiMiddleware();

            // ============================================================
            // Starta applikationen
            // ============================================================
            app.Run();
        }
    }
}