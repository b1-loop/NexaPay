// ============================================================
// AccountConfiguration.cs
// NexaPay.Infrastructure/Persistence/Configurations
// ============================================================
// Konfigurerar hur Account-entiteten mappas till databasen.
// IEntityTypeConfiguration<Account> är EF Cores interface
// för entitetskonfigurationer.
// ============================================================

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexaPay.Domain.Entities;

namespace NexaPay.Infrastructure.Persistence.Configurations
{
    // IEntityTypeConfiguration<Account> = konfigurera Account-tabellen
    public class AccountConfiguration : IEntityTypeConfiguration<Account>
    {
        // Configure anropas av EF Core via ApplyConfigurationsFromAssembly
        public void Configure(EntityTypeBuilder<Account> builder)
        {
            // --------------------------------------------------------
            // Tabellnamn
            // --------------------------------------------------------
            // Sätt explicit tabellnamn – annars använder EF Core
            // DbSet-namnet som är "Accounts" i det här fallet
            builder.ToTable("Accounts");

            // --------------------------------------------------------
            // Primary Key
            // --------------------------------------------------------
            // Id från BaseEntity är vår primärnyckel
            builder.HasKey(a => a.Id);

            // --------------------------------------------------------
            // Kolumnkonfigurationer
            // --------------------------------------------------------

            // AccountNumber – unikt kontonummer
            builder.Property(a => a.AccountNumber)
                // IsRequired = NOT NULL i databasen
                .IsRequired()
                // Max 30 tecken – "SE" + 18 siffror = 20 tecken
                // Vi ger lite extra marginal
                .HasMaxLength(30);

            // AccountName – kontonamn
            builder.Property(a => a.AccountName)
                .IsRequired()
                .HasMaxLength(100);

            // Balance – saldo
            // VIKTIGT: Precision och scale för decimal-kolumner!
            // precision = totalt antal siffror (18)
            // scale = antal decimaler (2)
            // T.ex. 9999999999999999.99 – rimligt för ett banksaldo
            builder.Property(a => a.Balance)
                .IsRequired()
                // HasPrecision(18, 2) = 18 siffror totalt, 2 decimaler
                .HasPrecision(18, 2);

            // AccountType – enum lagras som int i databasen
            builder.Property(a => a.AccountType)
                .IsRequired();

            // IsActive – boolean, required
            builder.Property(a => a.IsActive)
                .IsRequired();

            // OwnerId – Foreign Key till användartabellen
            builder.Property(a => a.OwnerId)
                .IsRequired()
                // ASP.NET Identity använder string-ID av variabel längd
                .HasMaxLength(450);

            // CreatedAt – tidsstämpel
            builder.Property(a => a.CreatedAt)
                .IsRequired();

            // UpdatedAt – nullable tidsstämpel
            // IsRequired(false) = tillåt NULL i databasen
            builder.Property(a => a.UpdatedAt)
                .IsRequired(false);

            // --------------------------------------------------------
            // Index
            // --------------------------------------------------------
            // Index på AccountNumber för snabb sökning
            // IsUnique() garanterar att samma kontonummer inte finns två gånger
            builder.HasIndex(a => a.AccountNumber)
                .IsUnique();

            // Index på OwnerId för snabb filtrering av en användares konton
            builder.HasIndex(a => a.OwnerId);

            // --------------------------------------------------------
            // Relationer
            // --------------------------------------------------------
            // En Account har många Transactions
            builder.HasMany(a => a.Transactions)
                // En Transaction tillhör ett Account
                .WithOne(t => t.Account)
                // Foreign Key i Transactions-tabellen
                .HasForeignKey(t => t.AccountId)
                // Om kontot tas bort → ta bort alla transaktioner
                // Cascade = automatisk borttagning av relaterade rader
                .OnDelete(DeleteBehavior.Cascade);

            // En Account har många Cards
            builder.HasMany(a => a.Cards)
                .WithOne(c => c.Account)
                .HasForeignKey(c => c.AccountId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}