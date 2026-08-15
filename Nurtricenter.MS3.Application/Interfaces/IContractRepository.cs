using Joseco.DDD.Core.Abstractions;
using Nurtricenter.MS3.Core.Aggregates;

namespace Nurtricenter.MS3.Application.Interfaces;

public interface IContractRepository : IRepository<Contract>
{
    Task<Contract?> GetByPatientIdAsync(Guid patientId, CancellationToken cancellationToken = default);
    Task<Contract?> GetByInvoiceNumberAsync(string invoiceNumber, CancellationToken cancellationToken = default);
}
