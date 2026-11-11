using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Stockly.Api.Infrastructure;
using Stockly.Application.Common;
using Stockly.Application.Features.Products;

namespace Stockly.Api.Controllers;

[ApiController]
[Route("api/products")]
public sealed class ProductsController(ProductService products, IBusinessClock clock) : ControllerBase
{
    /// <summary>Paged search. Sort keys: sku, name, category, price, stock, updatedAt (prefix "-" for descending).</summary>
    [HttpGet]
    public Task<PagedResult<ProductListItemDto>> Search([FromQuery] ProductQuery query, CancellationToken ct) => products.SearchAsync(query, ct);

    /// <summary>Same filters as the search, as a CSV file.</summary>
    [HttpGet("export")]
    [Produces("text/csv")]
    public async Task<FileContentResult> Export([FromQuery] ProductQuery query, CancellationToken ct) =>
        FileResults.Csv(await products.ExportCsvAsync(query, ct), "productos", clock.Today);

    /// <summary>Active products whose total stock is at or below their minimum.</summary>
    [HttpGet("low-stock")]
    public Task<IReadOnlyList<LowStockItemDto>> LowStock([FromQuery] int take = 20, CancellationToken ct = default) => products.LowStockAsync(take, ct);

    [HttpGet("{id:int}")]
    public Task<ProductDetailDto> Get(int id, CancellationToken ct) => products.GetAsync(id, ct);

    [HttpPost]
    [Authorize(Policy = Policies.ManageInventory)]
    public async Task<ActionResult<ProductDetailDto>> Create(CreateProductRequest request, CancellationToken ct)
    {
        var product = await products.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = product.Id }, product);
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = Policies.ManageInventory)]
    public Task<ProductDetailDto> Update(int id, UpdateProductRequest request, CancellationToken ct) => products.UpdateAsync(id, request, ct);
}
