using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Nurtricenter.MS3.Application.Simulations;
using Nurtricenter.MS3.Infrastructure.Data;
using Nurtricenter.MS3.Infrastructure.Messaging;
using Nurtricenter.MS3.Infrastructure.Messaging.Consumers;
using Nurtricenter.MS3.Infrastructure.Messaging.Contracts.Clinic;
using Nurtricenter.MS3.Infrastructure.Messaging.Contracts.DeliveryCalendar;

namespace Nurtricenter.MS3.IntegrationTests.Messaging;

/// <summary>
/// The projections that replaced MS3's REST calls to MS1 (patients) and MS4 (next-day deliveries), and the
/// simulation decorator that serves them.
/// </summary>
public sealed class ProjectionConsumersTests : IDisposable
{
    private static readonly DateOnly Day = new(2026, 11, 2);

    private readonly BaseDeDatosEnMemoria _db = new();

    public void Dispose() => _db.Dispose();

    private async Task DeliverAsync(ProductionOrderReady message)
    {
        await using var context = _db.CrearContexto();
        var consumer = new IdempotentConsumer<ProductionOrderReady, ProductionOrderReadyConsumer>(
            new ProductionOrderReadyConsumer(context, NullLogger<ProductionOrderReadyConsumer>.Instance),
            context,
            NullLogger<IdempotentConsumer<ProductionOrderReady, ProductionOrderReadyConsumer>>.Instance);
        await consumer.HandleAsync(message);
    }

    private async Task DeliverAsync(PatientPublished message)
    {
        await using var context = _db.CrearContexto();
        var consumer = new IdempotentConsumer<PatientPublished, PatientPublishedConsumer>(
            new PatientPublishedConsumer(context, NullLogger<PatientPublishedConsumer>.Instance),
            context,
            NullLogger<IdempotentConsumer<PatientPublished, PatientPublishedConsumer>>.Instance);
        await consumer.HandleAsync(message);
    }

    private ProjectedDataSimulationService Service(ApplicationDbContext context)
        => new(new SimulationDataService(), context);

    private static ProductionOrderReadyLine Line(int dayId, Guid? patientId = null, string? hour = null) => new()
    {
        DeliveryDayId = dayId,
        PatientId = patientId ?? Guid.NewGuid(),
        PlanId = Guid.NewGuid(),
        Hour = hour,
        Address = "Av. Banzer 123, Santa Cruz",
        Latitude = -17.78,
        Longitude = -63.18,
        Menu =
        [
            new ProductionOrderReadyMenuItem { MealTime = "Dinner", MealTimeOrder = 5, RecipeId = Guid.NewGuid(), RecipeName = "Majadito", Rations = 1 },
            new ProductionOrderReadyMenuItem { MealTime = "Lunch", MealTimeOrder = 3, RecipeId = Guid.NewGuid(), RecipeName = "Peanut soup", Rations = 2 }
        ]
    };

    private static ProductionOrderReady Order(DateOnly date, DateTimeOffset? createdAt = null, params ProductionOrderReadyLine[] lines)
    {
        var message = new ProductionOrderReady { ProductionOrderId = 1, DeliveryDate = date, ProductionDate = date.AddDays(-1), Lines = lines.ToList() };
        return createdAt is null ? message : message with { CreatedAt = createdAt.Value };
    }

    [Fact]
    public async Task ProductionOrderReady_is_served_as_active_calendars_with_the_menu_ordered_by_meal_time()
    {
        var line = Line(101, hour: "09:00");
        await DeliverAsync(Order(Day, null, line));

        await using var context = _db.CrearContexto();
        var calendar = Service(context).GetActiveCalendars(Day.ToDateTime(TimeOnly.MinValue)).Should().ContainSingle().Subject;

        calendar.PackageId.Should().Be(ProjectedDataSimulationService.PackIdToGuid(101));
        calendar.DeliveryDayId.Should().Be(101);
        calendar.PatientId.Should().Be(line.PatientId);
        calendar.PlanId.Should().Be(line.PlanId);
        calendar.Address.Should().Be("Av. Banzer 123, Santa Cruz");
        calendar.Latitude.Should().Be(-17.78);
        calendar.DeliveryTime.Should().Be("09:00");
        calendar.Menu!.Select(m => m.RecipeName).Should().Equal("Peanut soup", "Majadito");
        calendar.Menu![0].Rations.Should().Be(2);
    }

    [Fact]
    public async Task A_date_with_no_published_order_has_no_active_calendars_not_the_sample_data()
    {
        await using var context = _db.CrearContexto();

        Service(context).GetActiveCalendars(Day.ToDateTime(TimeOnly.MinValue)).Should().BeEmpty();
    }

    [Fact]
    public async Task A_new_snapshot_replaces_the_rows_of_its_date_and_leaves_other_dates_alone()
    {
        var otherDay = Day.AddDays(1);
        var t0 = DateTimeOffset.UtcNow;
        await DeliverAsync(Order(otherDay, t0, Line(300)));
        await DeliverAsync(Order(Day, t0, Line(101), Line(102)));

        await DeliverAsync(Order(Day, t0.AddSeconds(5), Line(102), Line(103)));

        await using var context = _db.CrearContexto();
        var service = Service(context);
        service.GetActiveCalendars(Day.ToDateTime(TimeOnly.MinValue)).Select(c => c.DeliveryDayId).Should().Equal(102, 103);
        service.GetActiveCalendars(otherDay.ToDateTime(TimeOnly.MinValue)).Select(c => c.DeliveryDayId).Should().Equal(300);
    }

    [Fact]
    public async Task A_snapshot_older_than_the_stored_one_is_ignored()
    {
        var t0 = DateTimeOffset.UtcNow;
        await DeliverAsync(Order(Day, t0.AddSeconds(5), Line(102)));

        await DeliverAsync(Order(Day, t0, Line(101)));

        await using var context = _db.CrearContexto();
        Service(context).GetActiveCalendars(Day.ToDateTime(TimeOnly.MinValue)).Select(c => c.DeliveryDayId).Should().Equal(102);
    }

    [Fact]
    public async Task A_rescheduled_day_moves_to_its_new_date_without_a_key_clash()
    {
        var t0 = DateTimeOffset.UtcNow;
        await DeliverAsync(Order(Day, t0, Line(101)));

        await DeliverAsync(Order(Day.AddDays(1), t0.AddSeconds(5), Line(101)));

        await using var context = _db.CrearContexto();
        (await context.ScheduledDeliveries.SingleAsync(d => d.DeliveryDayId == 101)).DeliveryDate.Should().Be(Day.AddDays(1));
    }

    [Fact]
    public async Task The_same_order_delivered_twice_is_processed_once()
    {
        var message = Order(Day, null, Line(101));

        await DeliverAsync(message);
        await DeliverAsync(message);

        await using var context = _db.CrearContexto();
        (await context.InboxMessages.CountAsync(m => m.MessageId == message.Id)).Should().Be(1);
        (await context.ScheduledDeliveries.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task PatientPublished_is_served_with_full_name_and_phone()
    {
        var patientId = Guid.NewGuid();
        await DeliverAsync(new PatientPublished { PatientId = patientId, DocumentNumber = "123", FirstName = "Juan", LastName = "Perez", Phone = "70012345" });

        await using var context = _db.CrearContexto();
        var patient = Service(context).GetPatient(patientId)!;

        patient.FullName.Should().Be("Juan Perez");
        patient.Phone.Should().Be("70012345");
        patient.ClinicalStatus.Should().Be("active");
    }

    [Fact]
    public async Task An_unknown_patient_is_not_found_even_if_the_sample_data_knows_the_id()
    {
        await using var context = _db.CrearContexto();

        Service(context).GetPatient(Guid.Parse("00000000-0000-0000-0000-000000000001")).Should().BeNull();
    }

    [Fact]
    public async Task A_republished_patient_refreshes_the_projection_and_an_older_one_is_ignored()
    {
        var patientId = Guid.NewGuid();
        var t0 = DateTimeOffset.UtcNow;
        await DeliverAsync(new PatientPublished { PatientId = patientId, FirstName = "Juan", LastName = "Perez", Phone = "1", CreatedAt = t0 });

        await DeliverAsync(new PatientPublished { PatientId = patientId, FirstName = "Juan", LastName = "Perez Gomez", Phone = "2", CreatedAt = t0.AddSeconds(5) });
        await DeliverAsync(new PatientPublished { PatientId = patientId, FirstName = "Old", LastName = "Name", Phone = "3", CreatedAt = t0.AddSeconds(2) });

        await using var context = _db.CrearContexto();
        var patient = Service(context).GetPatient(patientId)!;
        patient.FullName.Should().Be("Juan Perez Gomez");
        patient.Phone.Should().Be("2");
        (await context.ClinicPatients.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Plans_and_recipes_are_delegated_to_the_inner_service()
    {
        await using var context = _db.CrearContexto();
        var service = Service(context);

        service.GetPlan(Guid.Parse("10000000-0000-0000-0000-000000000001"))!.DurationDays.Should().Be(30);
        service.GetPlanStructures([Guid.Parse("10000000-0000-0000-0000-000000000001")]).Should().ContainSingle();
        service.GetRecipeName(Guid.NewGuid()).Should().BeNull();
    }
}
