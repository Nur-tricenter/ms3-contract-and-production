using Microsoft.EntityFrameworkCore;
using Nurtricenter.MS3.Application.Interfaces;
using Nurtricenter.MS3.Core.Aggregates;
using Nurtricenter.MS3.Infrastructure.Data;

namespace Nurtricenter.MS3.Infrastructure.Repositories;

public sealed class ContractRepository : IContractRepository
{
    private readonly ApplicationDbContext _context;

    public ContractRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Contract?> GetByIdAsync(Guid id, bool throwIfNotFound = false)
    {
        return await _context.Contracts
            .FirstOrDefaultAsync(c => c.Id == id);
    }

    public async Task<Contract?> GetByPatientIdAsync(Guid patientId, CancellationToken cancellationToken = default)
    {
        return await _context.Contracts
            .Where(c => c.PatientId == patientId)
            .OrderByDescending(c => c.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<Contract?> GetByInvoiceNumberAsync(string invoiceNumber, CancellationToken cancellationToken = default)
    {
        return await _context.Contracts
            .Where(c => c.Invoice != null && c.Invoice.InvoiceNumber == invoiceNumber)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task AddAsync(Contract entity)
    {
        await _context.Contracts.AddAsync(entity);
    }
}
