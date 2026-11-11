namespace Stockly.Application.Common;

public static class TransactionExtensions
{
    /// <summary>
    /// Runs <paramref name="work"/> inside a database transaction, joining the ambient one if it
    /// already exists. If anything throws, nothing is committed (stock, numbering and documents
    /// are always consistent with each other).
    /// </summary>
    public static async Task<T> InTransactionAsync<T>(this IAppDbContext db, Func<Task<T>> work, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is not null)
        {
            return await work();
        }

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var result = await work();
        await transaction.CommitAsync(ct);
        return result;
    }
}
