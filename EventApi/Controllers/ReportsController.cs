using EventApi.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EventApi.Controllers;

[Authorize(Roles = "Staff,Admin")]
[Route("api/reports")]
public class ReportsController : ApiControllerBase
{
    private readonly IReportService _reportService;

    public ReportsController(IReportService reportService) => _reportService = reportService;

    [HttpGet("event/{id:int}")]
    public async Task<IActionResult> GetEventReport(int id) =>
        ToActionResult(await _reportService.GetEventReportAsync(id));

    [HttpGet("events")]
    public async Task<IActionResult> GetEventsReport(
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10) =>
        ToActionResult(await _reportService.GetEventsReportAsync(status, page, pageSize));

    [HttpGet("overview")]
    public async Task<IActionResult> GetOverview() =>
        ToActionResult(await _reportService.GetOverviewAsync());
}
