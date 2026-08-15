using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nurtricenter.MS3.Core.Simulations;

namespace Nurtricenter.MS3.Infrastructure.Data.Configurations;

public sealed class OutgoingIntegrationEventConfiguration : IEntityTypeConfiguration<OutgoingIntegrationEvent>
{
    public void Configure(EntityTypeBuilder<OutgoingIntegrationEvent> builder)
    {
        builder.ToTable("OutgoingIntegrationEvents");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();

        builder.Property(e => e.EventType).IsRequired().HasMaxLength(100);
        builder.Property(e => e.TargetService).IsRequired().HasMaxLength(50);
        builder.Property(e => e.Payload).IsRequired().HasColumnType("jsonb");
        builder.Property(e => e.Status).IsRequired().HasMaxLength(50);
        builder.Property(e => e.CreatedAt).IsRequired();

        builder.HasIndex(e => e.EventType);
        builder.HasIndex(e => e.CreatedAt);
    }
}
