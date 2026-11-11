using FluentAssertions;
using Stockly.Application.Common;

namespace Stockly.UnitTests.Application;

public class SortMapTests
{
    private sealed record Item(string Name, decimal Price);

    private static readonly IQueryable<Item> Items = new[]
    {
        new Item("b", 20), new Item("a", 30), new Item("c", 10),
    }.AsQueryable();

    private static readonly SortMap<Item> Map = new SortMap<Item>("name").Add("name", i => i.Name).Add("price", i => i.Price);

    [Theory]
    [InlineData(null, "a,b,c")]
    [InlineData("price", "c,b,a")]
    [InlineData("-price", "a,b,c")]
    [InlineData("-NAME", "c,b,a")]
    [InlineData("password", "a,b,c")] // unknown keys fall back to the default (whitelist)
    public void Sorts_by_whitelisted_keys(string? sort, string expected) =>
        string.Join(',', Map.Apply(Items, sort).Select(i => i.Name)).Should().Be(expected);

    [Theory]
    [InlineData(0, 0, 1, 1)]
    [InlineData(3, 500, 3, PageRequest.MaxPageSize)]
    [InlineData(-5, 20, 1, 20)]
    public void Page_request_is_clamped(int page, int size, int expectedPage, int expectedSize)
    {
        var request = new PageRequest { Page = page, PageSize = size };
        request.SafePage.Should().Be(expectedPage);
        request.SafePageSize.Should().Be(expectedSize);
    }

    [Fact]
    public void Paged_result_computes_total_pages() => new PagedResult<int>([], 1, 20, 41).TotalPages.Should().Be(3);
}
