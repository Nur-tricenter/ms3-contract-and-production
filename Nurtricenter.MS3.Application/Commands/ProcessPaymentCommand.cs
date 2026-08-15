using Joseco.DDD.Core.Results;
using MediatR;
using Nurtricenter.MS3.Application.Dtos.Billing;

namespace Nurtricenter.MS3.Application.Commands;

public sealed record ProcessPaymentCommand(
    Guid ContractId,
    decimal Amount,
    string InvoiceNumber
) : IRequest<Result<PaymentResponse>>;
