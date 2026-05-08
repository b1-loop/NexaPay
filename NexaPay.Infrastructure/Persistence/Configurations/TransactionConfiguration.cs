using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexaPay.Domain.Entities;
using NexaPay.Domain.Enums;

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
                    .IsRequired()
                    .HasDefaultValue(Currency.SEK);
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
                    .IsRequired()
                    .HasDefaultValue(Currency.SEK);
            });

            builder.Property(t => t.ReceiverAccountId)
                .IsRequired(false);

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

            // Filtered unique index: only non-NULL keys are checked for uniqueness.
            // NULL (no key supplied) rows are excluded so legacy inserts still work.
            builder.HasIndex(t => t.IdempotencyKey)
                .IsUnique()
                .HasFilter("[IdempotencyKey] IS NOT NULL");
        }
    }
}
