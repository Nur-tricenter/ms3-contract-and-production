using FastEndpoints;
using Joseco.DDD.Core.Abstractions;
using MediatR;
using Nurtricenter.MS3.Application.Commands;
using Nurtricenter.MS3.Application.Dtos.Production;
using Nurtricenter.MS3.Application.Interfaces;

namespace Nurtricenter.MS3.Api.Endpoints.Production;

public sealed class ValidatePackageEndpoint : Endpoint<ApproveQualityRequest, ValidationResponse>
{
    private readonly IPackageRepository _packageRepository;
    private readonly IMediator _mediator;

    public ValidatePackageEndpoint(IPackageRepository packageRepository, IMediator mediator)
    {
        _packageRepository = packageRepository;
        _mediator = mediator;
    }

    public override void Configure()
    {
        Post("/api/v1/production/packages/validate");
        Description(d => d
            .WithTags("Production")
            .WithSummary("Valida y aprueba el control de calidad de los paquetes de una orden de producción")
            .Produces<ValidationResponse>(200)
            .ProducesProblem(400)
            .ProducesProblem(404));
        AllowAnonymous();
    }

    public override async Task HandleAsync(ApproveQualityRequest req, CancellationToken ct)
    {
        // 1. Buscar paquetes
        var packages = await _packageRepository.GetByProductionOrderIdAsync(req.ProductionOrderId, ct);
        if (packages.Count == 0)
        {
            await SendNotFoundAsync(ct);
            return;
        }

        // 2. Validar que el cliente confirmó el batch
        if (!req.BatchValidated)
        {
            AddError("BatchValidated must be true to confirm validation of all packages in the order.");
            await SendErrorsAsync(400, ct);
            return;
        }

        // 3. Validar conteo esperado vs real
        if (req.TotalValidatedCount > 0 && req.TotalValidatedCount != packages.Count)
        {
            AddError($"TotalValidatedCount mismatch: expected {req.TotalValidatedCount} but found {packages.Count} packages.");
            await SendErrorsAsync(400, ct);
            return;
        }

        // 4. Validar cada paquete individualmente via MediatR (usa ApproveQualityCommandHandler)
        var results = new List<PackageValidationResult>();
        foreach (var package in packages)
        {
            var command = new ApproveQualityCommand(package.Id, req.SupervisorId);
            var result = await _mediator.Send(command, ct);

            if (result.IsSuccess)
            {
                results.Add(new PackageValidationResult(package.Id, true, null));
            }
            else
            {
                results.Add(new PackageValidationResult(
                    package.Id,
                    false,
                    result.Error?.Description ?? "Unknown validation error"));
            }
        }

        // 5. Construir respuesta
        var validated = results.Count(r => r.Success);
        var failed = results.Count(r => !r.Success);

        var response = new ValidationResponse(
            req.ProductionOrderId,
            packages.Count,
            validated,
            failed,
            results,
            DateTime.UtcNow);

        await SendOkAsync(response, ct);
    }
}
