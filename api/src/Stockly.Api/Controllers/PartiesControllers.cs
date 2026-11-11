using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Stockly.Application.Common;
using Stockly.Application.Features.Parties;

namespace Stockly.Api.Controllers;

[ApiController]
[Route("api/customers")]
public sealed class CustomersController(CustomerService customers) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<CustomerDto>> Search([FromQuery] PartyQuery query, CancellationToken ct) => customers.SearchAsync(query, ct);

    [HttpGet("{id:int}")]
    public Task<CustomerDto> Get(int id, CancellationToken ct) => customers.GetAsync(id, ct);

    [HttpPost]
    [Authorize(Policy = Policies.Sell)]
    public async Task<ActionResult<CustomerDto>> Create(SaveCustomerRequest request, CancellationToken ct)
    {
        var customer = await customers.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = customer.Id }, customer);
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = Policies.Sell)]
    public Task<CustomerDto> Update(int id, SaveCustomerRequest request, CancellationToken ct) => customers.UpdateAsync(id, request, ct);
}

[ApiController]
[Route("api/suppliers")]
public sealed class SuppliersController(SupplierService suppliers) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<SupplierDto>> Search([FromQuery] PartyQuery query, CancellationToken ct) => suppliers.SearchAsync(query, ct);

    [HttpGet("{id:int}")]
    public Task<SupplierDto> Get(int id, CancellationToken ct) => suppliers.GetAsync(id, ct);

    [HttpPost]
    [Authorize(Policy = Policies.ManageInventory)]
    public async Task<ActionResult<SupplierDto>> Create(SaveSupplierRequest request, CancellationToken ct)
    {
        var supplier = await suppliers.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = supplier.Id }, supplier);
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = Policies.ManageInventory)]
    public Task<SupplierDto> Update(int id, SaveSupplierRequest request, CancellationToken ct) => suppliers.UpdateAsync(id, request, ct);
}
