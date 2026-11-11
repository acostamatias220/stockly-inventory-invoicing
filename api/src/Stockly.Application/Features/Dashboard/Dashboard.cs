using Microsoft.EntityFrameworkCore;
using Stockly.Application.Common;
using Stockly.Application.Features.Products;
using Stockly.Domain.Common;
using Stockly.Domain.Enums;

namespace Stockly.Application.Features.Dashboard;

public sealed record DashboardKpisDto(
    decimal SalesThisMonth, decimal SalesPreviousMonth, int InvoicesThisMonth, decimal Receivables,
    int OverdueInvoices, decimal InventoryValue, int LowStockProducts, int ActiveProducts);

public sealed record SalesPointDto(DateOnly Date, decimal Total, int Invoices);

public sealed record TopProductDto(int ProductId, string Sku, string Name, int Quantity, decimal Revenue);

public sealed record CategoryStockDto(int CategoryId, string Name, string Color, int Units, decimal Value);

public sealed record DashboardDto(
    DashboardKpisDto Kpis,
    IReadOnlyList<SalesPointDto> SalesByDay,
    IReadOnlyList<TopProductDto> TopProducts,
    IReadOnlyList<CategoryStockDto> StockByCategory,
    IReadOnlyList<LowStockItemDto> LowStock);

/// <summary>Read model for the home screen. Every aggregate is computed in SQL; only small result sets travel.</summary>
public sealed class DashboardService(IAppDbContext db, ProductService products, IBusinessClock clock)
{
    private static readonly InvoiceStatus[] Billed = [InvoiceStatus.Issued, InvoiceStatus.Paid];

    public async Task<DashboardDto> GetAsync(int days, CancellationToken ct)
    {
        days = Math.Clamp(days, 7, 365);
        var today = clock.Today;
        var from = today.AddDays(-(days - 1));
        var monthStart = new DateOnly(today.Year, today.Month, 1);
        var previousMonthStart = monthStart.AddMonths(-1);

        var billed = db.Invoices.AsNoTracking().Where(i => Billed.Contains(i.Status));

        var daily = await billed
            .Where(i => i.IssueDate >= from && i.IssueDate <= today)
            .GroupBy(i => i.IssueDate)
            .Select(g => new { Date = g.Key, Total = g.Sum(i => i.Total), Count = g.Count() })
            .ToListAsync(ct);
        var byDate = daily.Where(d => d.Date is not null).ToDictionary(d => d.Date!.Value);
        var salesByDay = Enumerable.Range(0, days)
            .Select(offset => from.AddDays(offset))
            .Select(date => byDate.TryGetValue(date, out var d) ? new SalesPointDto(date, d.Total, d.Count) : new SalesPointDto(date, 0, 0))
            .ToList();

        var topProducts = await db.InvoiceLines.AsNoTracking()
            .Where(l => db.Invoices.Any(i => i.Id == l.InvoiceId && Billed.Contains(i.Status) && i.IssueDate >= from))
            .GroupBy(l => new { l.ProductId, l.Sku, l.Description })
            .Select(g => new { g.Key.ProductId, g.Key.Sku, g.Key.Description, Quantity = g.Sum(l => l.Quantity), Revenue = g.Sum(l => l.NetAmount) })
            .OrderByDescending(p => p.Revenue)
            .Take(7)
            .ToListAsync(ct);

        var stockByCategory = await db.StockLevels.AsNoTracking()
            .GroupBy(s => new { s.Product.CategoryId, s.Product.Category.Name, s.Product.Category.Color })
            .Select(g => new { g.Key.CategoryId, g.Key.Name, g.Key.Color, Units = g.Sum(s => s.Quantity), Value = g.Sum(s => s.Quantity * s.Product.Cost) })
            .Where(c => c.Units > 0)
            .OrderByDescending(c => c.Value)
            .ToListAsync(ct);

        var monthSales = await billed.Where(i => i.IssueDate >= monthStart).SumAsync(i => (decimal?)i.Total, ct) ?? 0;
        var previousSales = await billed.Where(i => i.IssueDate >= previousMonthStart && i.IssueDate < monthStart).SumAsync(i => (decimal?)i.Total, ct) ?? 0;
        var monthCount = await billed.CountAsync(i => i.IssueDate >= monthStart, ct);
        var receivables = await db.Invoices.Where(i => i.Status == InvoiceStatus.Issued).SumAsync(i => (decimal?)i.Total, ct) ?? 0;
        var overdue = await db.Invoices.CountAsync(i => i.Status == InvoiceStatus.Issued && i.DueDate < today, ct);
        var lowStock = await products.LowStockAsync(8, ct);
        var lowStockCount = (await products.SearchAsync(new ProductQuery { LowStock = true, Active = true, PageSize = 1 }, ct)).TotalCount;
        var activeProducts = await db.Products.CountAsync(p => p.IsActive, ct);

        var kpis = new DashboardKpisDto(
            monthSales, previousSales, monthCount, receivables, overdue,
            Money.Round(stockByCategory.Sum(c => c.Value)), lowStockCount, activeProducts);

        return new DashboardDto(
            kpis,
            salesByDay,
            topProducts.Select(p => new TopProductDto(p.ProductId, p.Sku, p.Description, p.Quantity, p.Revenue)).ToList(),
            stockByCategory.Select(c => new CategoryStockDto(c.CategoryId, c.Name, c.Color, c.Units, Money.Round(c.Value))).ToList(),
            lowStock);
    }
}
