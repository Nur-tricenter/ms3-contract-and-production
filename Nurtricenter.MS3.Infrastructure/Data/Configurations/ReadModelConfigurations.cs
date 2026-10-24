using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nurtricenter.MS3.Infrastructure.Data.ReadModels;

namespace Nurtricenter.MS3.Infrastructure.Data.Configurations;

public sealed class CatalogPlanConfiguration : IEntityTypeConfiguration<CatalogPlan>
{
    public void Configure(EntityTypeBuilder<CatalogPlan> builder)
    {
        builder.ToTable("CatalogPlans");

        builder.HasKey(p => p.PlanId);
        builder.Property(p => p.PlanId).ValueGeneratedNever();
        builder.Property(p => p.Name).IsRequired().HasMaxLength(200);
        builder.Property(p => p.DurationType).IsRequired().HasMaxLength(50);
        builder.Property(p => p.TotalDays).IsRequired();
        builder.Property(p => p.MealsPerDay).IsRequired();
        builder.Property(p => p.UpdatedAt).IsRequired();
    }
}

public sealed class CatalogRecipeConfiguration : IEntityTypeConfiguration<CatalogRecipe>
{
    public void Configure(EntityTypeBuilder<CatalogRecipe> builder)
    {
        builder.ToTable("CatalogRecipes");

        builder.HasKey(r => r.RecipeId);
        builder.Property(r => r.RecipeId).ValueGeneratedNever();
        builder.Property(r => r.Name).IsRequired().HasMaxLength(200);
        builder.Property(r => r.Instructions).IsRequired();
        builder.Property(r => r.UpdatedAt).IsRequired();
    }
}

public sealed class InboxMessageConfiguration : IEntityTypeConfiguration<InboxMessage>
{
    public void Configure(EntityTypeBuilder<InboxMessage> builder)
    {
        builder.ToTable("InboxMessages");

        builder.HasKey(m => m.MessageId);
        builder.Property(m => m.MessageId).ValueGeneratedNever();
        builder.Property(m => m.MessageType).IsRequired().HasMaxLength(256);
        builder.Property(m => m.CorrelationId).HasMaxLength(128);
        builder.Property(m => m.MessageCreatedAt).IsRequired();
        builder.Property(m => m.ProcessedOn).IsRequired();

        builder.HasIndex(m => m.ProcessedOn);
    }
}
