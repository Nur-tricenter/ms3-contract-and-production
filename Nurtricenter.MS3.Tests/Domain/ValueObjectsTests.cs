using FluentAssertions;
using Nurtricenter.MS3.Core.ValueObjects;

namespace Nurtricenter.MS3.Tests.Domain;

public sealed class ValueObjectsTests
{
    [Fact]
    public void Label_compares_by_structural_equality()
    {
        var a = new Label("NTR-1234", "María López", "Av. Principal 123");
        var b = new Label("NTR-1234", "María López", "Av. Principal 123");

        a.Should().Be(b);
        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    [Fact]
    public void Label_equality_is_case_insensitive()
    {
        var a = new Label("ntr-1234", "MARÍA LÓPEZ", "av. principal 123");
        var b = new Label("NTR-1234", "maría lópez", "Av. Principal 123");

        a.Should().Be(b);
    }

    [Fact]
    public void Label_with_different_tracking_number_is_not_equal()
    {
        var a = new Label("NTR-1111", "María López", "Av. Principal 123");
        var b = new Label("NTR-2222", "María López", "Av. Principal 123");

        a.Should().NotBe(b);
    }

    [Fact]
    public void Label_rejects_blank_values()
    {
        var act = () => new Label(" ", "María López", "Av. Principal 123");

        act.Should().Throw<ArgumentException>().Which.ParamName.Should().Be("trackingNumber");
    }

    [Fact]
    public void ProductionItem_compares_by_structural_equality()
    {
        var recipeId = Guid.NewGuid();
        var a = new ProductionItem(recipeId, "Ensalada César", 10);
        var b = new ProductionItem(recipeId, "ensalada céSar", 10);

        a.Should().Be(b);
        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    [Fact]
    public void ProductionItem_rejects_invalid_arguments()
    {
        var recipeId = Guid.NewGuid();

        var emptyRecipe = () => new ProductionItem(Guid.Empty, "Ensalada", 10);
        var emptyName = () => new ProductionItem(recipeId, "", 10);
        var invalidQuantity = () => new ProductionItem(recipeId, "Ensalada", 0);

        emptyRecipe.Should().Throw<ArgumentException>();
        emptyName.Should().Throw<ArgumentException>();
        invalidQuantity.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void QualityValidation_approve_creates_approved_validation()
    {
        var validation = QualityValidation.Approve("SUP-99");

        validation.IsApproved.Should().BeTrue();
        validation.SupervisorId.Should().Be("SUP-99");
        validation.ValidatedAt.Should().NotBe(default);
    }

    [Fact]
    public void QualityValidation_reject_creates_rejected_validation()
    {
        var validation = QualityValidation.Reject("SUP-99");

        validation.IsApproved.Should().BeFalse();
    }

    [Fact]
    public void QualityValidation_rejects_empty_supervisor_id()
    {
        var act = () => QualityValidation.Approve(" ");

        act.Should().Throw<ArgumentException>().Which.ParamName.Should().Be("supervisorId");
    }

    [Fact]
    public void QualityValidation_equality_uses_all_components()
    {
        var validatedAt = new DateTime(2026, 9, 4, 10, 0, 0);
        var a = new QualityValidation(validatedAt, "SUP-99", true);
        var b = new QualityValidation(validatedAt, "SUP-99", true);
        var rejected = new QualityValidation(validatedAt, "SUP-99", false);

        a.Should().Be(b);
        a.Should().NotBe(rejected);
    }
}
