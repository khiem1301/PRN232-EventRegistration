using EventRegistration.WebClient.Models;
using EventRegistration.WebClient.Services;
using Microsoft.AspNetCore.Mvc;

namespace EventRegistration.WebClient.Controllers;

public class RegistrationsController : Controller
{
    private readonly EventApiClient _api;

    public RegistrationsController(EventApiClient api) => _api = api;

    private bool IsLoggedIn => !string.IsNullOrEmpty(HttpContext.Session.GetString("JwtToken"));
    private bool CanCheckIn => HttpContext.Session.GetString("UserRole") is "Staff" or "Admin";

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        if (!IsLoggedIn) return RedirectToAction("Login", "Auth");

        var registrations = await _api.GetAsync<List<RegistrationViewModel>>("/api/registrations/me");
        return View(registrations ?? new List<RegistrationViewModel>());
    }

    [HttpPost]
    public async Task<IActionResult> Register(int eventId)
    {
        if (!IsLoggedIn) return RedirectToAction("Login", "Auth");

        var result = await _api.PostAsync<RegistrationViewModel>("/api/registrations", new { eventId });
        if (!result.Success)
        {
            TempData["Error"] = result.Error ?? "Registration failed.";
            return RedirectToAction("Details", "Events", new { id = eventId });
        }

        TempData["Success"] = "Event registered successfully.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Cancel(int id, int eventId)
    {
        if (!IsLoggedIn) return RedirectToAction("Login", "Auth");

        var result = await _api.PostAsync<RegistrationViewModel>($"/api/registrations/{id}/cancel", new { });
        if (!result.Success)
        {
            TempData["Error"] = result.Error ?? "Cancellation failed.";
            return RedirectToAction(nameof(Index));
        }

        TempData["Success"] = "Registration cancelled successfully.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> CheckIn()
    {
        if (!CanCheckIn) return RedirectToAction("Login", "Auth");

        var pagedEvents = await _api.GetAsync<PagedEventsViewModel>("/api/events?status=&page=1&pageSize=100");
        var model = new List<RegistrationViewModel>();

        foreach (var evt in pagedEvents?.Items ?? new List<EventListItemViewModel>())
        {
            var eventRegs = await _api.GetAsync<List<RegistrationViewModel>>($"/api/registrations/events/{evt.Id}");
            if (eventRegs != null)
                model.AddRange(eventRegs);
        }

        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> CheckIn(int id, string status)
    {
        if (!CanCheckIn) return RedirectToAction("Login", "Auth");

        var result = await _api.PostAsync<RegistrationViewModel>($"/api/registrations/{id}/checkin", new { status });
        if (!result.Success)
        {
            TempData["Error"] = result.Error ?? "Check-in failed.";
            return RedirectToAction(nameof(CheckIn));
        }

        TempData["Success"] = "Attendance updated successfully.";
        return RedirectToAction(nameof(CheckIn));
    }
}
