using Joseco.DDD.Core.Abstractions;

namespace Nurtricenter.MS3.Core.Aggregates;

public sealed class ControlCharge : AggregateRoot
{
    public Guid PatientId { get; private set; }
    public Guid ControlPolicyId { get; private set; }
    public decimal Amount { get; private set; }
    public DateTime ChargedAt { get; private set; }
    public string InvoiceNumber { get; private set; }

    private ControlCharge() { } // EF Core

    private ControlCharge(Guid patientId, Guid controlPolicyId, decimal amount, string invoiceNumber)
    {
        Id = Guid.NewGuid();
        PatientId = patientId;
        ControlPolicyId = controlPolicyId;
        Amount = amount;
        ChargedAt = DateTime.UtcNow;
        InvoiceNumber = invoiceNumber;
    }

    /// <summary>
    /// Processes an independent control charge for a patient's follow-up consultation.
    /// The caller should first check internally whether the patient has an active catering
    /// contract to determine the correct amount before calling this method.
    /// </summary>
    public static ControlCharge ProcessControlCharge(Guid patientId, Guid controlPolicyId, decimal amount, string invoiceNumber)
    {
        if (patientId == Guid.Empty)
            throw new ArgumentException("PatientId cannot be empty.", nameof(patientId));
        if (controlPolicyId == Guid.Empty)
            throw new ArgumentException("ControlPolicyId cannot be empty.", nameof(controlPolicyId));
        if (amount <= 0)
            throw new ArgumentException("Amount must be greater than zero.", nameof(amount));
        if (string.IsNullOrWhiteSpace(invoiceNumber))
            throw new ArgumentException("InvoiceNumber cannot be empty.", nameof(invoiceNumber));

        return new ControlCharge(patientId, controlPolicyId, amount, invoiceNumber);
    }
}
