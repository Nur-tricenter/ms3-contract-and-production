using Joseco.DDD.Core.Abstractions;
using Joseco.DDD.Core.Results;
using MediatR;
using Nurtricenter.MS3.Application.Commands;
using Nurtricenter.MS3.Application.Interfaces;
using Nurtricenter.MS3.Core.Aggregates;

namespace Nurtricenter.MS3.Application.Handlers;

public sealed class ConsolidateDailyOrderCommandHandler : IRequestHandler<ConsolidateDailyOrderCommand, Result<DailyProductionOrder>>
{
    private readonly IDailyProductionOrderRepository _dailyProductionOrderRepository;
    private readonly IUnitOfWork _unitOfWork;

    public ConsolidateDailyOrderCommandHandler(
        IDailyProductionOrderRepository dailyProductionOrderRepository,
        IUnitOfWork unitOfWork)
    {
        _dailyProductionOrderRepository = dailyProductionOrderRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<DailyProductionOrder>> Handle(ConsolidateDailyOrderCommand request, CancellationToken cancellationToken)
    {
        var existing = await _dailyProductionOrderRepository.GetByProductionDateAsync(request.ProductionDate, cancellationToken);
        if (existing is not null)
            return (Result<DailyProductionOrder>)Result.Failure(
                new Error("DUPLICATE_ORDER", $"A production order already exists for {request.ProductionDate:yyyy-MM-dd}.", ErrorType.Conflict));

        var order = DailyProductionOrder.ConsolidateOrder(request.ProductionDate, request.Items);

        await _dailyProductionOrderRepository.AddAsync(order);
        await _unitOfWork.CommitAsync(cancellationToken);

        return Result<DailyProductionOrder>.Success(order);
    }
}
