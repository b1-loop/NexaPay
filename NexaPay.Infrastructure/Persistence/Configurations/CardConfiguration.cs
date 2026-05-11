// ============================================================
// CardConfiguration.cs
// NexaPay.Infrastructure/Persistence/Configurations
// ============================================================

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexaPay.Domain.Entities;
using NexaPay.Domain.Enums;

namespace NexaPay.Infrastructure.Persistence.Configurations
{
    public class CardConfiguration : IEntityTypeConfiguration<Card>
    {
        public void Configure(EntityTypeBuilder<Card> builder)
        {
            builder.ToTable("Cards");

            builder.HasKey(c => c.Id);

            // Opaque token som ersätter PAN i databasen
            builder.Property(c => c.CardToken)
                .IsRequired()
                .HasMaxLength(36);

            // Sista 4 siffror av PAN – för visning ("**** **** **** 9010")
            builder.Property(c => c.Last4Digits)
                .IsRequired()
                .HasMaxLength(4);

            // Kortinnehavarens namn – max 26 tecken (kortstandard)
            builder.Property(c => c.CardHolderName)
                .IsRequired()
                .HasMaxLength(26);

            // Utgångsdatum – DateOnly lagras som date i SQL Server
            builder.Property(c => c.ExpiryDate)
                .IsRequired();

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
            // Unikt index på token – tar rollen av den gamla PAN-index
            builder.HasIndex(c => c.CardToken)
                .IsUnique();

            // Index för snabb hämtning av alla kort per konto
            builder.HasIndex(c => c.AccountId);

            // Matchar Account-filtret – kort kopplade till stängda konton döljs automatiskt
            builder.HasQueryFilter(c => c.Account!.Status != AccountStatus.Closed);
        }
    }
}