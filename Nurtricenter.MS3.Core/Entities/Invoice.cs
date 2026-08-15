using Joseco.DDD.Core.Abstractions;

namespace Nurtricenter.MS3.Core.Entities;

public sealed class Invoice : Entity
{
    public string InvoiceNumber { get; private set; }
    public decimal TotalAmount { get; private set; }
    public bool IsPaid { get; private set; }

    private Invoice() { } // EF Core

    public Invoice(string invoiceNumber, decimal totalAmount)
    {
        if (string.IsNullOrWhiteSpace(invoiceNumber))
            throw new ArgumentException("InvoiceNumber cannot be empty.", nameof(invoiceNumber));
        if (totalAmount <= 0)
            throw new ArgumentException("TotalAmount must be greater than zero.", nameof(totalAmount));

        Id = Guid.NewGuid();
        InvoiceNumber = invoiceNumber;
        TotalAmount = totalAmount;
        IsPaid = false;
    }

    /// <summary>
    /// Marks the invoice as paid. Once paid, it cannot be reversed.
    /// </summary>
    public void MarkAsPaid()
    {
        if (IsPaid)
            throw new InvalidOperationException("Invoice is already marked as paid.");

        IsPaid = true;
    }
}
