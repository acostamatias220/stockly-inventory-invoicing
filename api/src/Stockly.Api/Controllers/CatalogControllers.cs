using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Stockly.Application.Common;
using Stockly.Application.Features.Catalog;

namespace Stockly.Api.Controllers;

[ApiController]
[Route("api/categories")]
public sealed class CategoriesController(CatalogService catalog) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<CategoryDto>> List(CancellationToken ct) => catalog.ListCategoriesAsync(ct);

    [HttpPost]
    [Authorize(Policy = Policies.ManageInventory)]
    public async Task<ActionResult<CategoryDto>> Create(SaveCategoryRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await catalog.CreateCategoryAsync(request, ct));

    [HttpPut("{id:int}")]
    [Authorize(Policy = Policies.ManageInventory)]
    public Task<CategoryDto> Update(int id, SaveCategoryRequest request, CancellationToken ct) => catalog.UpdateCategoryAsync(id, request, ct);

    [HttpDelete("{id:int}")]
    [Authorize(Policy = Policies.ManageInventory)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await catalog.DeleteCategoryAsync(id, ct);
        return NoContent();
    }
}

[ApiController]
[Route("api/warehouses")]
public sealed class WarehousesController(CatalogService catalog) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<WarehouseDto>> List(CancellationToken ct) => catalog.ListWarehousesAsync(ct);

    [HttpPost]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<ActionResult<WarehouseDto>> Create(CreateWarehouseRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await catalog.CreateWarehouseAsync(request, ct));

    [HttpPut("{id:int}")]
    [Authorize(Policy = Policies.AdminOnly)]
    public Task<WarehouseDto> Update(int id, UpdateWarehouseRequest request, CancellationToken ct) => catalog.UpdateWarehouseAsync(id, request, ct);
}
