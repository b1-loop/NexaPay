// ============================================================
// ServiceExtensions.cs – NexaPay.API
// ============================================================

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NexaPay.API.Middleware;
using NexaPay.Infrastructure.Persistence;

namespace NexaPay.API
{
    public static class ServiceExtensions
    {
        // --------------------------------------------------------
        // Identity – användare, lösenord och roller
        // --------------------------------------------------------
        public static IServiceCollection AddIdentityServices(
            this IServiceCollection services)
        {
            services.AddIdentity<IdentityUser, IdentityRole>(options =>
            {
                // Lösenordskrav
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = true;
                options.Password.RequiredLength = 8;

                // Kontolåsning efter misslyckade inloggningar
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan =
                    TimeSpan.FromMinutes(15);

                // Kräv unik e-post
                options.User.RequireUniqueEmail = true;
            })
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();

            return services;
        }

        // --------------------------------------------------------
        // API-tjänster – Controllers, Swagger, CORS
        // --------------------------------------------------------
        public static IServiceCollection AddApiServices(
            this IServiceCollection services)
        {
            // Controllers
            services.AddControllers();

            // Swagger – enkel konfiguration utan JWT
            services.AddEndpointsApiExplorer();
            services.AddSwaggerGen();

            // CORS
            services.AddCors(options =>
            {
                options.AddPolicy("AllowAll", policy =>
                {
                    policy
                        .AllowAnyOrigin()
                        .AllowAnyMethod()
                        .AllowAnyHeader();
                });
            });

            return services;
        }

        // --------------------------------------------------------
        // Middleware-pipeline
        // --------------------------------------------------------
        public static WebApplication UseApiMiddleware(
            this WebApplication app)
        {
            if (app.Environment.IsDevelopment())
            {
                // Swagger UI – nås på /swagger
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            // 1. Global felhantering – alltid först
            app.UseMiddleware<ExceptionMiddleware>();

            // 2. HTTPS
            app.UseHttpsRedirection();

            // 3. CORS
            app.UseCors("AllowAll");

            // 4. Authentication – vem är du?
            app.UseAuthentication();

            // 5. Authorization – vad får du göra?
            app.UseAuthorization();

            // 6. Controllers
            app.MapControllers();

            return app;
        }
    }
}