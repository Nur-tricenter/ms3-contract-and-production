using Joseco.DDD.Core.Results;
using MediatR;
using Nurtricenter.MS3.Application.Dtos.Contracts;

namespace Nurtricenter.MS3.Application.Queries;

public sealed record GetContractQuery(Guid ContractId) : IRequest<Result<ContractResponse>>;
