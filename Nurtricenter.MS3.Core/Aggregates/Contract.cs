using Joseco.DDD.Core.Abstractions;
using Nurtricenter.MS3.Core.DomainEvents;
using Nurtricenter.MS3.Core.Entities;
using Nurtricenter.MS3.Core.Enums;
using Nurtricenter.MS3.Core.ValueObjects;

namespace Nurtricenter.MS3.Core.Aggregates;

public sealed class Contract : AggregateRoot
{
    public Guid PatientId { get; private set; }
    public Guid CatalogPlanId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public ContractStatus Status { get; private set; }
    public Invoice? Invoice { get; private set; }

    private Contract() { } // EF Core

    private Contract(Guid patientId, Guid catalogPlanId)
    {
        Id = Guid.NewGuid();
        PatientId = patientId;
        CatalogPlanId = catalogPlanId;
        CreatedAt = DateTime.UtcNow;
        Status = ContractStatus.PendingPayment;
    }

    /// <summary>
    /// Factory method to create a new contract. The contract starts in PendingPayment status.
    /// After creation, the caller must validate with MS1 (patient exists) and MS2 (plan exists).
    /// </summary>
    public static Contract Create(Guid patientId, Guid catalogPlanId)
    {
        if (patientId == Guid.Empty)
            throw new ArgumentException("PatientId cannot be empty.", nameof(patientId));
        if (catalogPlanId == Guid.Empty)
            throw new ArgumentException("CatalogPlanId cannot be empty.", nameof(catalogPlanId));

        var contract = new Contract(patientId, catalogPlanId);

        contract.AddDomainEvent(new ContractCreatedDomainEvent(contract.Id, patientId, catalogPlanId));

        return contract;
    }

    /// <summary>
    /// Processes the payment for the contract plan and issues an invoice.
    /// Transitions the contract from PendingPayment to Active.
    /// </summary>
    public Invoice ProcessPayment(decimal amount, string invoiceNumber)
    {
        if (Status != ContractStatus.PendingPayment)
            throw new InvalidOperationException($"Cannot process payment. Contract is in '{Status}' state.");

        var invoice = new Invoice(invoiceNumber, amount);
        invoice.MarkAsPaid();
        Invoice = invoice;
        Status = ContractStatus.Active;

        AddDomainEvent(new ContractActivatedDomainEvent(Id, PatientId, CatalogPlanId));

        return invoice;
    }

    /// <summary>
    /// Cancels the contract with a specified reason.
    /// Only Active contracts can be canceled.
    /// </summary>
    public void CancelContract(string reason)
    {
        if (Status != ContractStatus.Active)
            throw new InvalidOperationException($"Cannot cancel contract. Contract is in '{Status}' state.");
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Reason cannot be empty.", nameof(reason));

        Status = ContractStatus.Canceled;

        AddDomainEvent(new ContractCanceledDomainEvent(Id, PatientId, reason));
    }
}
