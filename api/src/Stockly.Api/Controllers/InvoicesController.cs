using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Stockly.Api.Infrastructure;
using Stockly.Application.Common;
using Stockly.Application.Features.Invoices;

namespace Stockly.Api.Controllers;

[ApiController]
[Route("api/invoices")]
public sealed class InvoicesController(InvoiceService invoices, IBusinessClock clock) : ControllerBase
{
    /// <summary>Paged search. Sort keys: createdAt, number, issueDate, dueDate, customer, total, status.</summary>
    [HttpGet]
    public Task<PagedResult<InvoiceListItemDto>> Search([FromQuery] InvoiceQuery query, CancellationToken ct) => invoices.SearchAsync(query, ct);

    [HttpGet("export")]
    [Produces("text/csv")]
    public async Task<FileContentResult> Export([FromQuery] InvoiceQuery query, CancellationToken ct) =>
        FileResults.Csv(await invoices.ExportCsvAsync(query, ct), "facturas", clock.Today);

    [HttpGet("{id:int}")]
    public Task<InvoiceDetailDto> Get(int id, CancellationToken ct) => invoices.GetAsync(id, ct);

    /// <summary>Invoice as PDF (drafts and cancelled invoices are watermarked).</summary>
    [HttpGet("{id:int}/pdf")]
    [Produces("application/pdf")]
    public async Task<FileContentResult> Pdf(int id, CancellationToken ct)
    {
        var invoice = await invoices.GetAsync(id, ct);
        var bytes = await invoices.RenderPdfAsync(id, ct);
        return File(bytes, "application/pdf", $"factura-{invoice.Number ?? $"borrador-{id}"}.pdf");
    }

    /// <summary>Creates a draft. Prices default to the current product price; nothing touches stock yet.</summary>
    [HttpPost]
    [Authorize(Policy = Policies.Sell)]
    public async Task<ActionResult<InvoiceDetailDto>> Create(SaveInvoiceRequest request, CancellationToken ct)
    {
        var invoice = await invoices.CreateDraftAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = invoice.Id }, invoice);
    }

    /// <summary>Replaces a draft's header and lines. Send the <c>version</c> you edited to detect concurrent changes.</summary>
    [HttpPut("{id:int}")]
    [Authorize(Policy = Policies.Sell)]
    public Task<InvoiceDetailDto> Update(int id, SaveInvoiceRequest request, CancellationToken ct) => invoices.UpdateDraftAsync(id, request, ct);

    [HttpDelete("{id:int}")]
    [Authorize(Policy = Policies.Sell)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await invoices.DeleteDraftAsync(id, ct);
        return NoContent();
    }

    /// <summary>Draft → Issued: assigns the next number and takes the goods out of stock in one transaction.</summary>
    [HttpPost("{id:int}/issue")]
    [Authorize(Policy = Policies.Sell)]
    public Task<InvoiceDetailDto> Issue(int id, [FromQuery] int? version, CancellationToken ct) => invoices.IssueAsync(id, version, ct);

    /// <summary>Issued → Paid.</summary>
    [HttpPost("{id:int}/pay")]
    [Authorize(Policy = Policies.Sell)]
    public Task<InvoiceDetailDto> Pay(int id, PayInvoiceRequest request, CancellationToken ct) => invoices.PayAsync(id, request, ct);

    /// <summary>Draft/Issued → Cancelled. Cancelling an issued invoice (admin only) returns the goods to stock.</summary>
    [HttpPost("{id:int}/cancel")]
    [Authorize(Policy = Policies.Sell)]
    public Task<InvoiceDetailDto> Cancel(int id, CancelInvoiceRequest request, CancellationToken ct) => invoices.CancelAsync(id, request, ct);
}
