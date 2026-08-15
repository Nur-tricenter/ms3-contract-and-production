using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nurtricenter.MS3.Core.Aggregates;

namespace Nurtricenter.MS3.Infrastructure.Data.Configurations;

public sealed class ControlChargeConfiguration : IEntityTypeConfiguration<ControlCharge>
{
    public void Configure(EntityTypeBuilder<ControlCharge> builder)
    {
        builder.ToTable("ControlCharges");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id)
            .ValueGeneratedNever();

        builder.Property(c => c.PatientId)
            .IsRequired();

        builder.Property(c => c.ControlPolicyId)
            .IsRequired();

        builder.Property(c => c.Amount)
            .IsRequired()
            .HasColumnType("decimal(18,2)");

        builder.Property(c => c.ChargedAt)
            .IsRequired();

        builder.Property(c => c.InvoiceNumber)
            .IsRequired()
            .HasMaxLength(50);

        builder.HasIndex(c => c.PatientId);
    }
}
