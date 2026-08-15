using Joseco.DDD.Core.Results;
using MediatR;

namespace Nurtricenter.MS3.Application.Commands;

/// <summary>
/// Approves a package through quality control validation.
/// After approval, the package is ready for dispatch to MS5 Logistics.
/// </summary>
public sealed record ApproveQualityCommand(
    Guid PackageId,
    string SupervisorId
) : IRequest<Result>;
