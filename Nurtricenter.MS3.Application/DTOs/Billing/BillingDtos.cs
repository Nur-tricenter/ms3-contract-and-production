namespace Nurtricenter.MS3.Application.Dtos.Billing;

public sealed record ProcessPaymentRequest(
    Guid ContractId,
    decimal Amount,
    string InvoiceNumber
);

public sealed record PaymentResponse(
    Guid InvoiceId,
    string Status,
    DateTime TransactionDateTime
);

public sealed record InvoiceResponse(
    Guid InvoiceId,
    Guid PatientId,
    string PlanName,
    decimal TotalAmount,
    DateTime IssuedAt,
    bool IsPaid
);

public sealed record ProcessControlChargeRequest(
    Guid PatientId,
    Guid ControlPolicyId,
    decimal Amount,
    string InvoiceNumber
);

public sealed record ControlChargeResponse(
    string InvoiceId,
    string Status,
    decimal AmountCharged
);
