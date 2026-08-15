using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nurtricenter.MS3.Core.Aggregates;

namespace Nurtricenter.MS3.Infrastructure.Data.Configurations;

public sealed class ContractConfiguration : IEntityTypeConfiguration<Contract>
{
    public void Configure(EntityTypeBuilder<Contract> builder)
    {
        builder.ToTable("Contracts");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id)
            .ValueGeneratedNever();

        builder.Property(c => c.PatientId)
            .IsRequired();

        builder.Property(c => c.CatalogPlanId)
            .IsRequired();

        builder.Property(c => c.CreatedAt)
            .IsRequired();

        builder.Property(c => c.Status)
            .HasConversion<string>()
            .IsRequired()
            .HasMaxLength(30);

        // Owned entity: Invoice (stored in same table)
        builder.OwnsOne(c => c.Invoice, invoice =>
        {
            invoice.Property(i => i.Id)
                .ValueGeneratedNever();

            invoice.Property(i => i.InvoiceNumber)
                .IsRequired()
                .HasMaxLength(50);

            invoice.Property(i => i.TotalAmount)
                .IsRequired()
                .HasColumnType("decimal(18,2)");

            invoice.Property(i => i.IsPaid)
                .IsRequired();
        });

        // Index for querying by patient
        builder.HasIndex(c => c.PatientId);
    }
}
