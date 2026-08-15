using Joseco.DDD.Core.Results;
using MediatR;
using Nurtricenter.MS3.Application.Dtos.Billing;
using Nurtricenter.MS3.Core.Aggregates;

namespace Nurtricenter.MS3.Application.Commands;

/// <summary>
/// Processes an independent control charge for a patient's follow-up consultation.
/// The handler checks internally whether the patient has an active catering contract
/// to determine pricing.
/// </summary>
public sealed record ProcessControlChargeCommand(
    Guid PatientId,
    Guid ControlPolicyId,
    decimal Amount,
    string InvoiceNumber
) : IRequest<Result<ControlChargeResponse>>;
