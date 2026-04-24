// ============================================================
// CardConfiguration.cs
// NexaPay.Infrastructure/Persistence/Configurations
// ============================================================

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexaPay.Domain.Entities;

namespace NexaPay.Infrastructure.Persistence.Configurations
{
    public class CardConfiguration : IEntityTypeConfiguration<Card>
    {
        public void Configure(EntityTypeBuilder<Card> builder)
        {
            builder.ToTable("Cards");

            builder.HasKey(c => c.Id);

            // Kortnummer – 16 siffror
            builder.Property(c => c.CardNumber)
                .IsRequired()
                .HasMaxLength(16);

            // Kortinnehavarens namn – max 26 tecken (kortstandard)
            builder.Property(c => c.CardHolderName)
                .IsRequired()
                .HasMaxLength(26);

            // Utgångsdatum – DateOnly lagras som date i SQL Server
            builder.Property(c => c.ExpiryDate)
                .IsRequired();

            // CVV – 3 siffror
            // I verkligheten skulle detta vara krypterat
            builder.Property(c => c.CVV)
                .IsRequired()
                .HasMaxLength(3);

            // Status – enum lagras som int
            builder.Property(c => c.Status)
                .IsRequired();

            // Foreign Key till Account
            builder.Property(c => c.AccountId)
                .IsRequired();

            builder.Property(c => c.CreatedAt)
                .IsRequired();

            builder.Property(c => c.UpdatedAt)
                .IsRequired(false);

            // --------------------------------------------------------
            // Index
            // --------------------------------------------------------
            // Unikt index på kortnummer
            builder.HasIndex(c => c.CardNumber)
                .IsUnique();

            // Index för snabb hämtning av alla kort per konto
            builder.HasIndex(c => c.AccountId);
        }
    }
}