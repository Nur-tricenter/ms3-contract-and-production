using Joseco.DDD.Core.Results;
using MediatR;
using Nurtricenter.MS3.Application.Dtos.Production;

namespace Nurtricenter.MS3.Application.Queries;

public sealed record GetOrderPackagesQuery(Guid ProductionOrderId) : IRequest<Result<ProductionOrderPackagesResponse>>;
