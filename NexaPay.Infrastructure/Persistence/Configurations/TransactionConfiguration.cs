// ============================================================
// TransactionConfiguration.cs
// NexaPay.Infrastructure/Persistence/Configurations
// ============================================================
// Fluent API-konfiguration för Transaction-tabellen.
//
// Viktiga val:
//   * Amount och BalanceAfterTransaction är owned types (Money)
//     → 4 kolumner totalt (beloppen + valutorna).
//   * IdempotencyKey har FILTRERAT unikt index – endast non-NULL
//     värden tvingas unika. NULL-rader (gamla transaktioner utan
//     key) påverkas inte. Detta är hjärtat i dubbla-POST-skyddet.
//   * AccountId + CreatedAt indexeras för snabb historik-query.
// ============================================================

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexaPay.Domain.Entities;

namespace NexaPay.Infrastructure.Persistence.Configurations
{
    public class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
    {
        public void Configure(EntityTypeBuilder<Transaction> builder)
        {
            builder.ToTable("Transactions");
            builder.HasKey(t => t.Id);

            // Amount stored as two columns:
            //   "Amount"          – decimal (preserves existing column name)
            //   "Amount_Currency" – currency enum
            builder.OwnsOne(t => t.Amount, b =>
            {
                b.Property(m => m.Amount)
                    .HasColumnName("Amount")
                    .IsRequired()
                    .HasPrecision(18, 2);

                b.Property(m => m.Currency)
                    .HasColumnName("Amount_Currency")
                    .IsRequired();
            });

            builder.Property(t => t.Type)
                .IsRequired();

            builder.Property(t => t.Description)
                .IsRequired()
                .HasMaxLength(500);

            // BalanceAfterTransaction stored as two columns:
            //   "BalanceAfterTransaction"          – decimal
            //   "BalanceAfterTransaction_Currency" – currency enum
            builder.OwnsOne(t => t.BalanceAfterTransaction, b =>
            {
                b.Property(m => m.Amount)
                    .HasColumnName("BalanceAfterTransaction")
                    .IsRequired()
                    .HasPrecision(18, 2);

                b.Property(m => m.Currency)
                    .HasColumnName("BalanceAfterTransaction_Currency")
                    .IsRequired();
            });

            builder.Property(t => t.ReceiverAccountId)
                .IsRequired(false);

            // Sätts bara för fakturabetalningar – null för övriga transaktionstyper.
            builder.Property(t => t.Bankgiro)
                .IsRequired(false)
                .HasMaxLength(20);

            builder.Property(t => t.Ocr)
                .IsRequired(false)
                .HasMaxLength(25);

            builder.Property(t => t.AccountId)
                .IsRequired();

            builder.Property(t => t.CreatedAt)
                .IsRequired();

            builder.Property(t => t.UpdatedAt)
                .IsRequired(false);

            builder.Property(t => t.IdempotencyKey)
                .IsRequired(false);

            builder.HasIndex(t => t.AccountId);
            builder.HasIndex(t => t.CreatedAt);

            // Composite filtered unique index – idempotency-nyckeln är unik
            // PER KONTO, inte globalt. Detta hindrar User B från att råka
            // (eller avsiktligt) återanvända User A:s nyckel och få tillbaka
            // A:s transaktion. NULL-rader exkluderas så vanliga inserts (utan
            // Idempotency-Key) inte kolliderar.
            builder.HasIndex(t => new { t.IdempotencyKey, t.AccountId })
                .IsUnique()
                .HasFilter("[IdempotencyKey] IS NOT NULL");

            // Transaktioner är ett historiskt register – vi vill behålla dem även för stängda konton.
            // Markera navigationen som optional så EF inte varnar om det globala Account-filtret.
            builder.Navigation(t => t.Account).IsRequired(false);
        }
    }
}
