using Microsoft.AspNetCore.Mvc;
using Stockly.Application.Features.Dashboard;

namespace Stockly.Api.Controllers;

[ApiController]
[Route("api/dashboard")]
public sealed class DashboardController(DashboardService dashboard) : ControllerBase
{
    /// <summary>KPIs and chart series for the last <paramref name="days"/> days (7–365).</summary>
    [HttpGet]
    public Task<DashboardDto> Get([FromQuery] int days = 30, CancellationToken ct = default) => dashboard.GetAsync(days, ct);
}
