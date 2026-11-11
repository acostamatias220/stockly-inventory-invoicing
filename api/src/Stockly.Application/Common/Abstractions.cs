using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Stockly.Domain.Entities;
using Stockly.Domain.Enums;

namespace Stockly.Application.Common;

/// <summary>
/// Persistence port. The application layer queries through EF Core's LINQ abstractions
/// (provider agnostic); the concrete MySQL context lives in Infrastructure.
/// </summary>
public interface IAppDbContext
{
    DbSet<User> Users { get; }
    DbSet<Category> Categories { get; }
    DbSet<Warehouse> Warehouses { get; }
    DbSet<Product> Products { get; }
    DbSet<StockLevel> StockLevels { get; }
    DbSet<StockMovement> StockMovements { get; }
    DbSet<Customer> Customers { get; }
    DbSet<Supplier> Suppliers { get; }
    DbSet<Invoice> Invoices { get; }
    DbSet<InvoiceLine> InvoiceLines { get; }

    DatabaseFacade Database { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

/// <summary>Result of an atomic stock change. When <see cref="Success"/> is false, Balance is the available stock.</summary>
public readonly record struct StockChange(bool Success, int Balance);

/// <summary>
/// Atomic, concurrency-safe stock primitives. Implemented with single SQL statements so two
/// concurrent sales can never take the same unit (no read-modify-write race).
/// Must be called inside a transaction.
/// </summary>
public interface IStockStore
{
    /// <summary>Adds <paramref name="delta"/> (may be negative). Fails without changes if the result would be negative.</summary>
    Task<StockChange> TryApplyAsync(int productId, int warehouseId, int delta, CancellationToken ct);

    /// <summary>Locks the stock row (creating it if needed) and returns the current quantity.</summary>
    Task<int> LockAsync(int productId, int warehouseId, CancellationToken ct);
}

/// <summary>Hands out correlative invoice numbers per letter and point of sale.</summary>
public interface IInvoiceNumberGenerator
{
    /// <summary>Reserves the next number. Must run in the same transaction that issues the invoice.</summary>
    Task<long> NextAsync(InvoiceLetter letter, int pointOfSale, CancellationToken ct);
}

public interface ICurrentUser
{
    int UserId { get; }
    UserRole Role { get; }
}

/// <summary>Clock aware of the business time zone (Argentina) for invoice dates and reports.</summary>
public interface IBusinessClock
{
    DateTime UtcNow { get; }
    DateOnly Today { get; }
    DateOnly ToBusinessDate(DateTime utc);

    /// <summary>UTC instant at which the given business day starts (00:00 local).</summary>
    DateTime StartOfDayUtc(DateOnly date);
}

public interface IPasswordHasher
{
    string Hash(User user, string password);
    bool Verify(User user, string password);
}

public interface ITokenService
{
    (string Token, DateTime ExpiresAt) CreateAccessToken(User user);
}

public interface IInvoicePdfRenderer
{
    byte[] Render(Features.Invoices.InvoiceDetailDto invoice);
}
