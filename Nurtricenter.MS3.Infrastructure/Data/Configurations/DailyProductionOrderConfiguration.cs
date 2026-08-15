using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nurtricenter.MS3.Core.Aggregates;

namespace Nurtricenter.MS3.Infrastructure.Data.Configurations;

public sealed class DailyProductionOrderConfiguration : IEntityTypeConfiguration<DailyProductionOrder>
{
    public void Configure(EntityTypeBuilder<DailyProductionOrder> builder)
    {
        builder.ToTable("DailyProductionOrders");

        builder.HasKey(o => o.Id);

        builder.Property(o => o.Id)
            .ValueGeneratedNever();

        builder.Property(o => o.ProductionDate)
            .IsRequired();

        builder.Property(o => o.Status)
            .HasConversion<string>()
            .IsRequired()
            .HasMaxLength(30);

        // Owns many: ProductionItem collection stored in a separate table
        builder.OwnsMany(o => o.Items, item =>
        {
            item.ToTable("ProductionItems");

            item.WithOwner().HasForeignKey("DailyProductionOrderId");

            item.Property(i => i.RecipeId)
                .IsRequired();

            item.Property(i => i.RecipeName)
                .IsRequired()
                .HasMaxLength(200);

            item.Property(i => i.TotalQuantity)
                .IsRequired();
        });

        builder.HasIndex(o => o.ProductionDate).IsUnique();
    }
}
