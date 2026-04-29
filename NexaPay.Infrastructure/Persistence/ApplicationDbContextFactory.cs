// ============================================================
// ApplicationDbContextFactory.cs
// NexaPay.Infrastructure/Persistence
// ============================================================
// En factory-klass som EF Core använder vid design-time
// (när vi kör Add-Migration och Update-Database).
//
// Problemet utan denna klass:
// När EF Core kör migrations körs inte Program.cs
// vilket betyder att DI-containern inte är uppsatt.
// EF Core vet därför inte hur den ska skapa ApplicationDbContext
// eftersom den normalt sett skapas via DI.
//
// Lösningen:
// Vi implementerar IDesignTimeDbContextFactory<T> som ger
// EF Core ett alternativt sätt att skapa DbContext
// specifikt för design-time operationer som migrations.
//
// Flöde:
//   1. EF Core hittar denna klass automatiskt
//   2. Den anropar CreateDbContext()
//   3. Vi läser connection string från appsettings.json
//   4. Vi skapar och returnerar en ApplicationDbContext
// ============================================================

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace NexaPay.Infrastructure.Persistence
{
    // IDesignTimeDbContextFactory<ApplicationDbContext> är
    // EF Cores interface för design-time skapande av DbContext.
    // EF Core hittar denna klass automatiskt via reflektion
    // när den letar efter en DbContext att använda för migrations.
    public class ApplicationDbContextFactory
        : IDesignTimeDbContextFactory<ApplicationDbContext>
    {
        // CreateDbContext anropas av EF Core under migrations
        // "args" innehåller eventuella kommandoradsargument
        // men vi använder dem inte i vår implementation
        public ApplicationDbContext CreateDbContext(string[] args)
        {
            // --------------------------------------------------------
            // Steg 1: Hitta appsettings.json
            // --------------------------------------------------------
            // Vi söker på flera ställen för att inte vara
            // hårt beroende av en specifik mappstruktur.
            // Detta gör factory-klassen mer robust.
            var basePaths = new[]
            {
                // Alternativ 1: Nuvarande mapp
                // Fungerar när migrations körs direkt från Infrastructure
                Directory.GetCurrentDirectory(),

                // Alternativ 2: API-projektets mapp
                // Fungerar när migrations körs med -StartupProject NexaPay.API
                // vilket är det vanligaste fallet i vår setup
                Path.Combine(
                    Directory.GetCurrentDirectory(),
                    "..",
                    "NexaPay.API")
            };

            // Variabel för konfigurationen – null tills vi hittar filen
            IConfigurationRoot? configuration = null;

            // Iterera igenom möjliga sökvägar
            foreach (var basePath in basePaths)
            {
                // Bygg den fullständiga sökvägen till appsettings.json
                var configPath = Path.Combine(basePath, "appsettings.json");

                // Kontrollera om filen finns på denna sökväg
                if (File.Exists(configPath))
                {
                    // Filen hittades – bygg konfigurationen
                    configuration = new ConfigurationBuilder()
                        // Sätt basePath så att AddJsonFile vet var den ska leta
                        .SetBasePath(basePath)
                        // Läs appsettings.json
                        .AddJsonFile("appsettings.json")
                        .Build();

                    // Vi hittade filen – avbryt loopen
                    break;
                }
            }

            // --------------------------------------------------------
            // Steg 2: Hämta connection string
            // --------------------------------------------------------
            // Försök hämta connection string från konfigurationen
            // Om ingen konfiguration hittades – använd en fallback
            var connectionString = configuration
                ?.GetConnectionString("DefaultConnection")
                // Fallback connection string används bara om appsettings.json
                // inte hittades – t.ex. i CI/CD-miljöer utan config-filer
                ?? "Server=localhost;Database=NexaPayDb;Trusted_Connection=True;TrustServerCertificate=True";

            // --------------------------------------------------------
            // Steg 3: Skapa DbContextOptions
            // --------------------------------------------------------
            // DbContextOptionsBuilder bygger upp konfigurationen
            // för ApplicationDbContext
            var optionsBuilder =
                new DbContextOptionsBuilder<ApplicationDbContext>();

            // Konfigurera SQL Server som databasprovider
            // med den connection string vi hittade
            optionsBuilder.UseSqlServer(connectionString);

            // --------------------------------------------------------
            // Steg 4: Skapa och returnera ApplicationDbContext
            // --------------------------------------------------------
            // Skapa en ny instans av ApplicationDbContext
            // med de options vi byggde upp ovan
            return new ApplicationDbContext(optionsBuilder.Options);
        }
    }
}