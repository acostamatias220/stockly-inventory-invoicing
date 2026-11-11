using Microsoft.EntityFrameworkCore;
using Stockly.Application.Common;
using Stockly.Infrastructure.Persistence;

namespace Stockly.Infrastructure.Stock;

/// <summary>
/// Stock primitives implemented as single SQL statements. InnoDB keeps the touched row locked until the
/// surrounding transaction commits, so concurrent sales of the last unit are serialized and only one wins.
/// </summary>
internal sealed class MySqlStockStore(AppDbContext db) : IStockStore
{
    public async Task<StockChange> TryApplyAsync(int productId, int warehouseId, int delta, CancellationToken ct)
    {
        EnsureTransaction();

        if (delta >= 0)
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO stock_levels (product_id, warehouse_id, quantity, updated_at)
                VALUES ({productId}, {warehouseId}, {delta}, UTC_TIMESTAMP(6))
                ON DUPLICATE KEY UPDATE quantity = quantity + {delta}, updated_at = UTC_TIMESTAMP(6)
                """, ct);
            return new StockChange(true, await ReadAsync(productId, warehouseId, ct));
        }

        // Conditional decrement: affects 0 rows when there isn't enough stock (no read-modify-write race).
        var affected = await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE stock_levels
            SET quantity = quantity + {delta}, updated_at = UTC_TIMESTAMP(6)
            WHERE product_id = {productId} AND warehouse_id = {warehouseId} AND quantity >= {-delta}
            """, ct);

        return new StockChange(affected == 1, await ReadAsync(productId, warehouseId, ct));
    }

    public async Task<int> LockAsync(int productId, int warehouseId, CancellationToken ct)
    {
        EnsureTransaction();
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT IGNORE INTO stock_levels (product_id, warehouse_id, quantity, updated_at)
            VALUES ({productId}, {warehouseId}, 0, UTC_TIMESTAMP(6))
            """, ct);

        return await db.Database
            .SqlQuery<int>($"SELECT quantity AS Value FROM stock_levels WHERE product_id = {productId} AND warehouse_id = {warehouseId} FOR UPDATE")
            .SingleAsync(ct);
    }

    private async Task<int> ReadAsync(int productId, int warehouseId, CancellationToken ct) =>
        await db.Database
            .SqlQuery<int>($"SELECT quantity AS Value FROM stock_levels WHERE product_id = {productId} AND warehouse_id = {warehouseId}")
            .SingleOrDefaultAsync(ct);

    private void EnsureTransaction()
    {
        if (db.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("Stock changes must run inside a transaction.");
        }
    }
}

/// <summary>
/// Correlative numbering backed by the <c>invoice_sequences</c> table. The upsert takes a row lock that is
/// held until COMMIT: concurrent issues of the same series wait for each other, and a rollback gives the
/// number back, so there are no gaps or duplicates.
/// </summary>
internal sealed class MySqlInvoiceNumberGenerator(AppDbContext db) : IInvoiceNumberGenerator
{
    public async Task<long> NextAsync(Domain.Enums.InvoiceLetter letter, int pointOfSale, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("Invoice numbers must be reserved inside a transaction.");
        }

        var series = letter.ToString();
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO invoice_sequences (letter, point_of_sale, last_number)
            VALUES ({series}, {pointOfSale}, 1)
            ON DUPLICATE KEY UPDATE last_number = last_number + 1
            """, ct);

        return await db.Database
            .SqlQuery<long>($"SELECT last_number AS Value FROM invoice_sequences WHERE letter = {series} AND point_of_sale = {pointOfSale}")
            .SingleAsync(ct);
    }
}
