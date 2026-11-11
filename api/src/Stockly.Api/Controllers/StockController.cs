using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Stockly.Api.Infrastructure;
using Stockly.Application.Common;
using Stockly.Application.Features.Stock;

namespace Stockly.Api.Controllers;

[ApiController]
[Route("api/stock")]
public sealed class StockController(StockService stock, IBusinessClock clock) : ControllerBase
{
    /// <summary>Audit trail of stock movements (append-only).</summary>
    [HttpGet("movements")]
    public Task<PagedResult<StockMovementDto>> Movements([FromQuery] StockMovementQuery query, CancellationToken ct) => stock.SearchAsync(query, ct);

    [HttpGet("movements/export")]
    [Produces("text/csv")]
    public async Task<FileContentResult> Export([FromQuery] StockMovementQuery query, CancellationToken ct) =>
        FileResults.Csv(await stock.ExportCsvAsync(query, ct), "movimientos-stock", clock.Today);

    /// <summary>Registers an entry, exit or physical-count adjustment.</summary>
    [HttpPost("movements")]
    [Authorize(Policy = Policies.ManageInventory)]
    public async Task<ActionResult<IReadOnlyList<StockMovementDto>>> Register(StockMovementRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await stock.RegisterAsync(request, ct));

    /// <summary>Moves units between two warehouses atomically (two linked movements).</summary>
    [HttpPost("transfers")]
    [Authorize(Policy = Policies.ManageInventory)]
    public async Task<ActionResult<IReadOnlyList<StockMovementDto>>> Transfer(StockTransferRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await stock.TransferAsync(request, ct));
}
