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

// Seed-användare skapas vid uppstart om de inte finns
// Alla har EmailConfirmed = true för att kringgå SMTP-kravet i dev
// Lösenord: NexaPay1!
file static class SeedUsers
{
    // (email, roll) – e-post avgör om det är staff (@nexapay.com) eller kund
    public static readonly (string Email, string Role)[] All =
    [
        ("admin@nexapay.com",       Roles.Admin),
        ("bankmanager@nexapay.com", Roles.BankManager),
        ("teller@nexapay.com",      Roles.Teller),
        ("auditor@nexapay.com",     Roles.Auditor),
        ("user@test.com",           Roles.User),
    ];

    public const string Password = "NexaPay1!";
}

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
                // InMemory-databas (används i integrationstester) stödjer inte migrationer
                if (context.Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory")
                {
                    await context.Database.EnsureCreatedAsync();
                }
                else if (app.Environment.IsProduction())
                {
                    // Automatisk migration vid uppstart är riskabelt i produktion:
                    // vid horisontell skalning försöker flera instanser migrera
                    // simultant. Flytta detta steg till deploy-pipelinen när
                    // applikationen skalas ut.
                    app.Logger.LogWarning(
                        "Automatisk databasmigration körs vid uppstart. " +
                        "Flytta 'dotnet ef database update' till ett separat " +
                        "deploy-steg i produktion för att undvika konflikter " +
                        "vid horisontell skalning.");
                    await context.Database.MigrateAsync();
                }
                else
                {
                    await context.Database.MigrateAsync();
                }

                // ------------------------------------------------
                // Steg 2: Seed roller
                // ------------------------------------------------
                // Skapar alla roller om de inte redan finns
                // Körs vid varje uppstart men skapar bara
                // roller som saknas – idempotent operation
                await SeedRolesAsync(services);

                // ------------------------------------------------
                // Steg 3: Seed användare (dev-konton per roll)
                // ------------------------------------------------
                // Skapar ett konto per roll med EmailConfirmed = true
                // Körs bara om kontot inte redan finns – idempotent
                await SeedUsersAsync(services);

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
        // Seed – Skapa ett konto per roll
        // --------------------------------------------------------
        // Använder UserManager direkt för att sätta EmailConfirmed = true
        // utan att gå via AuthService (som annars kräver e-postverifiering)
        private static async Task SeedUsersAsync(IServiceProvider services)
        {
            var userManager = services.GetRequiredService<UserManager<IdentityUser>>();
            var logger = services
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger(nameof(DatabaseExtensions));

            foreach (var (email, role) in SeedUsers.All)
            {
                // Hoppa över om kontot redan finns
                if (await userManager.FindByEmailAsync(email) is not null)
                    continue;

                // Skapa Identity-användaren med EmailConfirmed = true
                // så att inloggning fungerar direkt utan SMTP
                var user = new IdentityUser
                {
                    UserName = email,
                    Email    = email,
                    // Sätts till true här – kringgår e-postbekräftelsekravet i dev
                    EmailConfirmed = true
                };

                var result = await userManager.CreateAsync(user, SeedUsers.Password);
                if (!result.Succeeded)
                {
                    var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                    logger.LogError("Kunde inte skapa seed-användare {Email}: {Errors}", email, errors);
                    continue;
                }

                // Tilldela rätt roll
                await userManager.AddToRoleAsync(user, role);

                logger.LogInformation("Seed-användare skapad: {Email} ({Role})", email, role);
            }
        }

        // --------------------------------------------------------
        // Seed – Skapa alla roller om de inte finns
        // --------------------------------------------------------
        private static async Task SeedRolesAsync(
            IServiceProvider services)
        {
            var roleManager = services
                .GetRequiredService<RoleManager<IdentityRole>>();

            var logger = services
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger(nameof(DatabaseExtensions));

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

                    logger.LogInformation("Roll skapad: {Role}", role);
                }
            }
        }
    }
}