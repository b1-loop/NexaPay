using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexaPay.Infrastructure.Persistence.Entities;

namespace NexaPay.Infrastructure.Persistence.Configurations
{
    public class OutboxEventConfiguration : IEntityTypeConfiguration<OutboxEvent>
    {
        public void Configure(EntityTypeBuilder<OutboxEvent> builder)
        {
            builder.ToTable("OutboxEvents");
            builder.HasKey(o => o.Id);

            builder.Property(o => o.EventTypeName)
                .IsRequired()
                .HasMaxLength(500);

            builder.Property(o => o.PayloadJson)
                .IsRequired();

            builder.Property(o => o.CreatedAt)
                .IsRequired();

            builder.Property(o => o.ProcessedAt)
                .IsRequired(false);

            builder.Property(o => o.Error)
                .IsRequired(false);

            // Filtrerat index – dispatchern slår bara upp oprocessade rader,
            // så ett index på de raderna är allt vi behöver. SQL Server
            // tillåter filter, övriga providers kan ignorera detta.
            builder.HasIndex(o => o.CreatedAt)
                .HasFilter("[ProcessedAt] IS NULL");
        }
    }
}
