using EventRegistration.WebClient.Models;
using EventRegistration.WebClient.Services;
using Microsoft.AspNetCore.Mvc;

namespace EventRegistration.WebClient.Controllers;

public class ReportsController : Controller
{
    private readonly EventApiClient _api;

    public ReportsController(EventApiClient api) => _api = api;

    private bool CanView =>
        HttpContext.Session.GetString("UserRole") is "Staff" or "Admin";

    [HttpGet]
    public async Task<IActionResult> Index(string? status, int page = 1)
    {
        if (!CanView) return RedirectToAction("Login", "Auth");

        var overview = await _api.GetAsync<OverviewReportViewModel>("/api/reports/overview");
        var events = await _api.GetAsync<PagedReportsViewModel>(
            $"/api/reports/events?status={Uri.EscapeDataString(status ?? "")}&page={page}&pageSize=10");

        var model = overview ?? new OverviewReportViewModel();
        model.EventReports = events?.Items ?? [];
        ViewBag.PagedReports = events;
        ViewBag.Status = status;

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> EventDetail(int id)
    {
        if (!CanView) return RedirectToAction("Login", "Auth");

        var report = await _api.GetAsync<EventReportViewModel>($"/api/reports/event/{id}");
        if (report is null) return NotFound();
        return View(report);
    }
}
