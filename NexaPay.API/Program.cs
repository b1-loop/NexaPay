// ============================================================
// Program.cs � NexaPay.API
// ============================================================
// Startpunkten f�r NexaPay API.
// Ren och minimal � allt ansvar �r utbrutit till
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
            // Registrera tj�nster � varje rad har ett tydligt ansvar
            // --------------------------------------------------------

            // Application-lagret: MediatR, AutoMapper, FluentValidation
            builder.Services.AddApplication();

            // Infrastructure-lagret: EF Core, Repositories, JWT
            builder.Services.AddInfrastructure(builder.Configuration);

            // Identity: Anv�ndare, l�senord och roller
            builder.Services.AddIdentityServices();

            // API: Controllers, Swagger, CORS
            builder.Services.AddApiServices(builder.Configuration);

            // ============================================================
            // Bygg applikationen
            // ============================================================
            var app = builder.Build();

            // --------------------------------------------------------
            // Initalisera databas � migrationer och seed-data
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