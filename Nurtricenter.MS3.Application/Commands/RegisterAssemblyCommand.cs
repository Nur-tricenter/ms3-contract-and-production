using Joseco.DDD.Core.Results;
using MediatR;

namespace Nurtricenter.MS3.Application.Commands;

/// <summary>
/// Registers the physical assembly of a delivery package by kitchen staff.
/// </summary>
public sealed record RegisterAssemblyCommand(
    Guid PackageId,
    string StaffId
) : IRequest<Result>;
