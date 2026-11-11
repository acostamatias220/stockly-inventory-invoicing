using Stockly.Domain.Common;
using Stockly.Domain.Enums;

namespace Stockly.Domain.Entities;

/// <summary>
/// Append-only audit trail of every stock change. Quantity is signed: positive = in, negative = out.
/// </summary>
public class StockMovement : Entity
{
    private StockMovement() { }

    public int ProductId { get; private set; }
    public Product Product { get; private set; } = null!;
    public int WarehouseId { get; private set; }
    public Warehouse Warehouse { get; private set; } = null!;
    public StockMovementType Type { get; private set; }
    public int Quantity { get; private set; }

    /// <summary>Stock of the product in that warehouse right after this movement.</summary>
    public int BalanceAfter { get; private set; }

    public string? Reason { get; private set; }

    /// <summary>Human readable reference, e.g. the invoice number or a supplier remito.</summary>
    public string? Reference { get; private set; }

    public int? InvoiceId { get; private set; }
    public int? SupplierId { get; private set; }
    public Supplier? Supplier { get; private set; }

    /// <summary>Groups the two legs of a transfer (or all lines of an invoice).</summary>
    public Guid CorrelationId { get; private set; }

    public int UserId { get; private set; }
    public User User { get; private set; } = null!;
    public DateTime CreatedAt { get; private set; }

    public static StockMovement Record(
        int productId, int warehouseId, StockMovementType type, int quantity, int balanceAfter,
        int userId, Guid correlationId, DateTime now,
        string? reason = null, string? reference = null, int? invoiceId = null, int? supplierId = null)
    {
        if (quantity == 0)
        {
            throw new DomainException("stock.zero_quantity", "Un movimiento de stock no puede tener cantidad cero.");
        }

        if (balanceAfter < 0)
        {
            throw new DomainException("stock.negative_balance", "El stock resultante no puede ser negativo.");
        }

        return new StockMovement
        {
            ProductId = productId,
            WarehouseId = warehouseId,
            Type = type,
            Quantity = quantity,
            BalanceAfter = balanceAfter,
            UserId = userId,
            CorrelationId = correlationId,
            CreatedAt = now,
            Reason = reason?.Trim(),
            Reference = reference?.Trim(),
            InvoiceId = invoiceId,
            SupplierId = supplierId,
        };
    }
}
