// ============================================================
// ApplicationDbContext.cs – NexaPay.Infrastructure/Persistence
// ============================================================
// Entity Framework Cores DbContext för NexaPay.
// Representerar databasen som ett C#-objekt.
//
// DbSet<T> = en tabell i databasen representerad som en C#-lista
// T.ex. DbSet<Account> = Accounts-tabellen
//
// Vi ärver från DbContext som är EF Cores basklass.
// ============================================================

using Microsoft.EntityFrameworkCore;
using NexaPay.Domain.Entities;

namespace NexaPay.Infrastructure.Persistence
{
    public class ApplicationDbContext : DbContext
    {
        // --------------------------------------------------------
        // Konstruktor
        // --------------------------------------------------------
        // DbContextOptions innehåller konfiguration som:
        //   - Vilken databas vi ansluter till (SQL Server)
        //   - Connection string
        //   - Loggningsinställningar
        // Dessa options injiceras via DI när applikationen startar
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options) // Skicka options till DbContext-basklassen
        {
        }

        // --------------------------------------------------------
        // DbSets – representerar tabeller i databasen
        // --------------------------------------------------------
        // Varje DbSet<T> motsvarar en tabell.
        // EF Core använder dessa för att generera SQL-frågor.
        // "null!" säger till kompilatorn att vi vet att detta
        // inte är null – EF Core initierar dem automatiskt.

        // Accounts-tabellen – alla bankkonton i systemet
        public DbSet<Account> Accounts { get; set; } = null!;

        // Cards-tabellen – alla bankkort i systemet
        public DbSet<Card> Cards { get; set; } = null!;

        // Transactions-tabellen – alla finansiella händelser
        public DbSet<Transaction> Transactions { get; set; } = null!;

        // --------------------------------------------------------
        // OnModelCreating – konfigurera tabellerna
        // --------------------------------------------------------
        // Denna metod anropas av EF Core när den bygger upp
        // sin interna modell av databasen.
        // Vi använder den för att tillämpa våra Configuration-klasser.
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Anropa basklassens implementation först
            base.OnModelCreating(modelBuilder);

            // Tillämpa alla konfigurationer automatiskt
            // ApplyConfigurationsFromAssembly skannar detta assembly
            // och hittar alla klasser som implementerar
            // IEntityTypeConfiguration<T> automatiskt
            modelBuilder.ApplyConfigurationsFromAssembly(
                typeof(ApplicationDbContext).Assembly);

            // --------------------------------------------------------
            // Global filter – visa bara aktiva konton som standard
            // --------------------------------------------------------
            // HasQueryFilter lägger till ett WHERE-villkor på ALLA
            // frågor mot Accounts-tabellen automatiskt.
            // account.IsActive == true läggs till i varje SQL-fråga.
            // Admin kan använda .IgnoreQueryFilters() för att se alla.
            modelBuilder.Entity<Account>()
                .HasQueryFilter(a => a.IsActive);
        }
    }
}