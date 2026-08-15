using Joseco.DDD.Core.Results;
using MediatR;

namespace Nurtricenter.MS3.Application.Commands;

public sealed record GenerateLabelCommand(
    Guid PackageId,
    Guid PatientId
) : IRequest<Result>;
