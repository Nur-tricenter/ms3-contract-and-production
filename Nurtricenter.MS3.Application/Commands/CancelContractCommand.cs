using Joseco.DDD.Core.Results;
using MediatR;

namespace Nurtricenter.MS3.Application.Commands;

/// <summary>
/// Cancels an active contract with a specified reason.
/// </summary>
public sealed record CancelContractCommand(
    Guid ContractId,
    string Reason
) : IRequest<Result>;
