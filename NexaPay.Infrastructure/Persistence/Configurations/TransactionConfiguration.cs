// ============================================================
// TransactionConfiguration.cs
// NexaPay.Infrastructure/Persistence/Configurations
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

            // Belopp – precision 18, 2 decimaler (samma som Balance)
            builder.Property(t => t.Amount)
                .IsRequired()
                .HasPrecision(18, 2);

            // Transaktionstyp – enum som int
            builder.Property(t => t.Type)
                .IsRequired();

            // Beskrivning – max 500 tecken
            builder.Property(t => t.Description)
                .IsRequired()
                .HasMaxLength(500);

            // Saldo efter transaktionen – precision 18, 2
            builder.Property(t => t.BalanceAfterTransaction)
                .IsRequired()
                .HasPrecision(18, 2);

            // ReceiverAccountId – nullable (bara satt vid överföringar)
            builder.Property(t => t.ReceiverAccountId)
                .IsRequired(false);

            // AccountId – Foreign Key till Account
            builder.Property(t => t.AccountId)
                .IsRequired();

            builder.Property(t => t.CreatedAt)
                .IsRequired();

            builder.Property(t => t.UpdatedAt)
                .IsRequired(false);

            // --------------------------------------------------------
            // Index
            // --------------------------------------------------------
            // Index på AccountId – används ofta när vi hämtar
            // alla transaktioner för ett specifikt konto
            builder.HasIndex(t => t.AccountId);

            // Index på CreatedAt – används för sortering i kontoutdrag
            builder.HasIndex(t => t.CreatedAt);
        }
    }
}