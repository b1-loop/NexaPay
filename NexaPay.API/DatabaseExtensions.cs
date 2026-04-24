// ============================================================
// DatabaseExtensions.cs – NexaPay.API
// ============================================================
// Hanterar databasmigration och seed-data vid uppstart.
// ============================================================

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NexaPay.Infrastructure.Persistence;

namespace NexaPay.API
{
    public static class DatabaseExtensions
    {
        // Kör migrationer och seed-data vid uppstart
        public static async Task InitialiseDatabaseAsync(
            this WebApplication app)
        {
            using var scope = app.Services.CreateScope();
            var services = scope.ServiceProvider;

            try
            {
                // Hämta DbContext och kör migrationer
                var context = services
                    .GetRequiredService<ApplicationDbContext>();

                // Skapar databasen om den inte finns
                // och kör alla väntande migrationer
                await context.Database.MigrateAsync();

                // Seed roller
                await SeedRolesAsync(services);

                app.Logger.LogInformation(
                    "Databas initialiserad framgångsrikt");
            }
            catch (Exception ex)
            {
                app.Logger.LogError(ex,
                    "Ett fel uppstod under databasinitialisering");
                throw;
            }
        }

        // Skapa Admin och User roller om de inte finns
        private static async Task SeedRolesAsync(
            IServiceProvider services)
        {
            var roleManager = services
                .GetRequiredService<RoleManager<IdentityRole>>();

            // Roller som ska finnas i systemet
            var roles = new[] { "Admin", "User" };

            foreach (var role in roles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    await roleManager.CreateAsync(
                        new IdentityRole(role));
                }
            }
        }
    }
}