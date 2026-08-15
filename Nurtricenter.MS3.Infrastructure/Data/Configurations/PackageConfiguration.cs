using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nurtricenter.MS3.Core.Aggregates;

namespace Nurtricenter.MS3.Infrastructure.Data.Configurations;

public sealed class PackageConfiguration : IEntityTypeConfiguration<Package>
{
    public void Configure(EntityTypeBuilder<Package> builder)
    {
        builder.ToTable("Packages");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id)
            .ValueGeneratedNever();

        builder.Property(p => p.ProductionOrderId)
            .IsRequired();

        builder.Property(p => p.PatientId)
            .IsRequired();

        builder.Property(p => p.CatalogPlanId)
            .IsRequired();

        builder.Property(p => p.Status)
            .HasConversion<string>()
            .IsRequired()
            .HasMaxLength(30);

        // Owned Value Object: Label
        builder.OwnsOne(p => p.Label, label =>
        {
            label.Property(l => l.TrackingNumber)
                .HasMaxLength(100);

            label.Property(l => l.PatientName)
                .HasMaxLength(200);

            label.Property(l => l.DeliveryAddress)
                .HasMaxLength(500);
        });

        // Owned Value Object: QualityValidation
        builder.OwnsOne(p => p.Validation, validation =>
        {
            validation.Property(v => v.ValidatedAt);

            validation.Property(v => v.SupervisorId)
                .HasMaxLength(100);

            validation.Property(v => v.IsApproved);
        });

        builder.HasIndex(p => p.ProductionOrderId);
        builder.HasIndex(p => p.PatientId);
    }
}
