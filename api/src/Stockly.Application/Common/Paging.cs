using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;

namespace Stockly.Application.Common;

/// <summary>
/// Common query-string contract for list endpoints: <c>?page=1&amp;pageSize=20&amp;sort=-createdAt&amp;search=foo</c>.
/// A leading "-" in <see cref="Sort"/> means descending.
/// </summary>
public record PageRequest
{
    public const int MaxPageSize = 100;

    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string? Sort { get; init; }
    public string? Search { get; init; }

    public int SafePage => Math.Max(1, Page);
    public int SafePageSize => Math.Clamp(PageSize, 1, MaxPageSize);
}

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}

/// <summary>Whitelist of sortable columns for an entity (prevents sorting by arbitrary/unindexed fields).</summary>
public sealed class SortMap<T>(string defaultKey, bool defaultDescending = false)
{
    private readonly Dictionary<string, LambdaExpression> _keys = new(StringComparer.OrdinalIgnoreCase);

    public SortMap<T> Add<TKey>(string name, Expression<Func<T, TKey>> key)
    {
        _keys[name] = key;
        return this;
    }

    public IReadOnlyCollection<string> Keys => _keys.Keys;

    public IQueryable<T> Apply(IQueryable<T> query, string? sort)
    {
        var descending = defaultDescending;
        var name = defaultKey;

        if (!string.IsNullOrWhiteSpace(sort))
        {
            var trimmed = sort.Trim();
            var candidate = trimmed.TrimStart('-', '+');
            if (_keys.ContainsKey(candidate))
            {
                name = candidate;
                descending = trimmed.StartsWith('-');
            }
        }

        var expression = _keys[name];
        var method = descending ? nameof(Queryable.OrderByDescending) : nameof(Queryable.OrderBy);
        var call = Expression.Call(
            typeof(Queryable), method, [typeof(T), expression.ReturnType], query.Expression, Expression.Quote(expression));
        return query.Provider.CreateQuery<T>(call);
    }
}

public static class PagingExtensions
{
    public static async Task<PagedResult<TDto>> ToPagedAsync<T, TDto>(
        this IQueryable<T> query, PageRequest request, Expression<Func<T, TDto>> projection, CancellationToken ct)
    {
        var total = await query.CountAsync(ct);
        var items = await query
            .Skip((request.SafePage - 1) * request.SafePageSize)
            .Take(request.SafePageSize)
            .Select(projection)
            .ToListAsync(ct);
        return new PagedResult<TDto>(items, request.SafePage, request.SafePageSize, total);
    }
}
