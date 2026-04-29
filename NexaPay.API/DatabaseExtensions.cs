// ============================================================
// DatabaseExtensions.cs – NexaPay.API
// ============================================================
// Hanterar databasmigration och seed-data vid uppstart.
// Bruten ut från Program.cs för att hålla det rent.
//
// Ansvar:
//   1. Kör alla väntande EF Core-migrationer
//   2. Skapar databasen om den inte finns
//   3. Seedar alla roller vid första uppstarten
// ============================================================

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NexaPay.Application.Common.Constants;
using NexaPay.Infrastructure.Persistence;

namespace NexaPay.API
{
    public static class DatabaseExtensions
    {
        // --------------------------------------------------------
        // Kör migrationer och seed-data vid uppstart
        // --------------------------------------------------------
        // Anropas från Program.cs med:
        //   await app.InitialiseDatabaseAsync();
        //
        // "this WebApplication app" = extension method på WebApplication
        // Det gör att vi kan anropa den direkt på app-objektet
        public static async Task InitialiseDatabaseAsync(
            this WebApplication app)
        {
            // CreateScope skapar en ny DI-scope
            // Krävs för att hämta Scoped-tjänster utanför en request
            // "using" garanterar att scope städas upp efteråt
            using var scope = app.Services.CreateScope();
            var services = scope.ServiceProvider;

            try
            {
                // ------------------------------------------------
                // Steg 1: Kör EF Core-migrationer
                // ------------------------------------------------
                // Hämta DbContext från DI-containern
                var context = services
                    .GetRequiredService<ApplicationDbContext>();

                // MigrateAsync gör två saker:
                //   1. Skapar databasen om den inte finns
                //   2. Kör alla väntande migrationer
                // Det är säkert att köra flera gånger –
                // redan körda migrationer hoppas över
                await context.Database.MigrateAsync();

                // ------------------------------------------------
                // Steg 2: Seed roller
                // ------------------------------------------------
                // Skapar alla roller om de inte redan finns
                // Körs vid varje uppstart men skapar bara
                // roller som saknas – idempotent operation
                await SeedRolesAsync(services);

                // Logga att allt gick bra
                app.Logger.LogInformation(
                    "Databas initialiserad framgångsrikt");
            }
            catch (Exception ex)
            {
                // Logga felet om något går fel
                app.Logger.LogError(ex,
                    "Ett fel uppstod under databasinitialisering");

                // Kasta om felet – vi vill inte starta applikationen
                // med en felaktig eller saknad databas
                // Det är bättre att krascha tidigt än att få
                // konstiga fel senare
                throw;
            }
        }

        // --------------------------------------------------------
        // Seed – Skapa alla roller om de inte finns
        // --------------------------------------------------------
        // "private static" = bara tillgänglig i denna klass
        // Anropas bara från InitialiseDatabaseAsync
        private static async Task SeedRolesAsync(
            IServiceProvider services)
        {
            var roleManager = services
                .GetRequiredService<RoleManager<IdentityRole>>();

            // ------------------------------------------------
            // Alla roller i NexaPay-systemet
            // ------------------------------------------------
            // Läggs till automatiskt vid uppstart om de saknas
            // Vi använder konstanter från Roles-klassen för att
            // undvika stavfel och hårdkodade strängar
            var roles = new[]
            {
                // Full åtkomst till hela systemet
                Roles.Admin,

                // Bankchef – kan se allt och blockera kort
                Roles.BankManager,

                // Bankpersonal – kan hjälpa kunder
                Roles.Teller,

                // Revisor – kan bara läsa, inga skrivoperationer
                Roles.Auditor,

                // Vanlig kund – ser bara sina egna konton
                Roles.User
            };

            foreach (var role in roles)
            {
                // Kontrollera om rollen redan finns
                // RoleExistsAsync returnerar true om rollen finns
                if (!await roleManager.RoleExistsAsync(role))
                {
                    // Skapa rollen om den inte finns
                    await roleManager.CreateAsync(
                        new IdentityRole(role));

                    // Logga att rollen skapades
                    // Vi kan inte använda app.Logger här eftersom
                    // vi är i en statisk metod utan tillgång till app
                    // Så vi använder Console.WriteLine som fallback
                    Console.WriteLine(
                        $"Roll skapad: {role}");
                }
            }
        }
    }
}