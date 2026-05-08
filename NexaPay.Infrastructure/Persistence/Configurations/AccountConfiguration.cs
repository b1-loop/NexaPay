using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexaPay.Domain.Entities;
using NexaPay.Domain.Enums;

namespace NexaPay.Infrastructure.Persistence.Configurations
{
    public class AccountConfiguration : IEntityTypeConfiguration<Account>
    {
        public void Configure(EntityTypeBuilder<Account> builder)
        {
            builder.ToTable("Accounts");
            builder.HasKey(a => a.Id);

            builder.Property(a => a.AccountNumber)
                .IsRequired()
                .HasMaxLength(30);

            builder.Property(a => a.AccountName)
                .IsRequired()
                .HasMaxLength(100);

            // Balance is a Money value object stored as two columns:
            //   "Balance"         – the decimal amount (preserves existing column name)
            //   "AccountCurrency" – the currency enum
            builder.OwnsOne(a => a.Balance, b =>
            {
                b.Property(m => m.Amount)
                    .HasColumnName("Balance")
                    .IsRequired()
                    .HasPrecision(18, 2);

                b.Property(m => m.Currency)
                    .HasColumnName("AccountCurrency")
                    .IsRequired()
                    .HasDefaultValue(Currency.SEK);
            });

            builder.Property(a => a.AccountType)
                .IsRequired();

            builder.Property(a => a.Status)
                .IsRequired()
                .HasDefaultValue(AccountStatus.Open);

            builder.Property(a => a.OwnerId)
                .IsRequired()
                .HasMaxLength(450);

            builder.Property(a => a.CreatedAt)
                .IsRequired();

            builder.Property(a => a.UpdatedAt)
                .IsRequired(false);

            builder.Property(a => a.RowVersion)
                .IsRowVersion();

            builder.HasIndex(a => a.AccountNumber)
                .IsUnique();

            builder.HasIndex(a => a.OwnerId);

            // Transactions are an immutable audit ledger — restrict cascade delete
            // so they can never be wiped by accident when an account is removed.
            builder.HasMany(a => a.Transactions)
                .WithOne(t => t.Account)
                .HasForeignKey(t => t.AccountId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasMany(a => a.Cards)
                .WithOne(c => c.Account)
                .HasForeignKey(c => c.AccountId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
