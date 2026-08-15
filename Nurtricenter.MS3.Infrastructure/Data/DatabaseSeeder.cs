using Nurtricenter.MS3.Core.Aggregates;
using Nurtricenter.MS3.Core.ValueObjects;

namespace Nurtricenter.MS3.Infrastructure.Data;

public static class DatabaseSeeder
{
    public static async Task SeedAsync(ApplicationDbContext context)
    {
        if (context.Contracts.Any())
            return; // Already seeded

        // ── Sample Contracts ──────────────────────────────────────────
        var patient1Id = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var patient2Id = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var planPremiumId = Guid.Parse("10000000-0000-0000-0000-000000000001");
        var planBasicId = Guid.Parse("10000000-0000-0000-0000-000000000002");

        var contract1 = Contract.Create(patient1Id, planPremiumId);
        contract1.ProcessPayment(350.00m, "inv-2026-0091");

        var contract2 = Contract.Create(patient2Id, planBasicId);
        contract2.ProcessPayment(200.00m, "inv-2026-0092");

        var contract3 = Contract.Create(patient1Id, planBasicId);

        await context.Contracts.AddRangeAsync(contract1, contract2, contract3);

        // ── Sample Control Charge ─────────────────────────────────────
        var controlCharge = ControlCharge.ProcessControlCharge(
            Guid.Parse("00000000-0000-0000-0000-000000000003"),
            Guid.Parse("40000000-0000-0000-0000-000000000001"),
            30.00m,
            "inv-ctrl-5512");

        await context.ControlCharges.AddAsync(controlCharge);

        // ── Sample Daily Production Order ─────────────────────────────
        var items = new List<ProductionItem>
        {
            new(Guid.Parse("20000000-0000-0000-0000-000000000001"), "Grilled Chicken Breast with Quinoa", 45),
            new(Guid.Parse("20000000-0000-0000-0000-000000000004"), "Spinach Egg-White Omelet", 30)
        };

        var order = DailyProductionOrder.ConsolidateOrder(DateTime.UtcNow.AddDays(1).Date, items);
        await context.DailyProductionOrders.AddAsync(order);

        // ── Sample Packages ───────────────────────────────────────────
        var package1 = Package.Create(order.Id, patient1Id, planPremiumId);
        package1.RegisterAssembly("cook-09");

        var package2 = Package.Create(order.Id, patient2Id, planBasicId);

        await context.Packages.AddRangeAsync(package1, package2);

        await context.SaveChangesAsync();
    }
}
