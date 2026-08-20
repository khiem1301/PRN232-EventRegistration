using EventRegistration.WebClient.Models;
using EventRegistration.WebClient.Services;
using Microsoft.AspNetCore.Mvc;

namespace EventRegistration.WebClient.Controllers;

public class EventsController : Controller
{
    private readonly EventApiClient _api;

    public EventsController(EventApiClient api) => _api = api;

    private bool CanManage =>
        HttpContext.Session.GetString("UserRole") is "Staff" or "Admin";

    [HttpGet]
    public async Task<IActionResult> Index(
        string? search, string? status, string? sortBy, string? sortDir,
        int page = 1, int pageSize = 10,
        DateTime? startDate = null, DateTime? endDate = null)
    {
        var query = new List<string>();
        if (!string.IsNullOrWhiteSpace(search)) query.Add($"search={Uri.EscapeDataString(search)}");
        if (!string.IsNullOrWhiteSpace(status)) query.Add($"status={Uri.EscapeDataString(status)}");
        if (!string.IsNullOrWhiteSpace(sortBy)) query.Add($"sortBy={Uri.EscapeDataString(sortBy)}");
        if (!string.IsNullOrWhiteSpace(sortDir)) query.Add($"sortDir={Uri.EscapeDataString(sortDir)}");
        query.Add($"page={page}");
        query.Add($"pageSize={pageSize}");
        if (startDate.HasValue) query.Add($"startDate={startDate:O}");
        if (endDate.HasValue) query.Add($"endDate={endDate:O}");

        var url = $"/api/events?{string.Join("&", query)}";
        var result = await _api.GetAsync<PagedEventsViewModel>(url);

        var model = result ?? new PagedEventsViewModel();
        model.Search = search;
        model.Status = status;
        model.SortBy = sortBy ?? "createdAt";
        model.SortDir = sortDir ?? "desc";
        model.StartDate = startDate;
        model.EndDate = endDate;

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var evt = await _api.GetAsync<EventDetailViewModel>($"/api/events/{id}");
        if (evt is null) return NotFound();
        return View(evt);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        if (!CanManage) return RedirectToAction("Login", "Auth");
        return View(await BuildCreateViewModel(new CreateEventViewModel()));
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateEventViewModel model)
    {
        if (!CanManage) return RedirectToAction("Login", "Auth");

        var result = await _api.PostAsync<EventDetailViewModel>("/api/events", new
        {
            model.Title,
            model.Description,
            model.StartTime,
            model.EndTime,
            model.RegistrationDeadline,
            model.Capacity,
            model.LocationId,
            model.OrganizerId
        });

        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Create failed.");
            model = await BuildCreateViewModel(model);
            return View(model);
        }

        TempData["Success"] = "Event created successfully.";
        return RedirectToAction(nameof(Details), new { id = result.Data!.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        if (!CanManage) return RedirectToAction("Login", "Auth");

        var evt = await _api.GetAsync<EventDetailViewModel>($"/api/events/{id}");
        if (evt is null) return NotFound();

        var model = new EditEventViewModel
        {
            Id = evt.Id,
            Title = evt.Title,
            Description = evt.Description,
            StartTime = evt.StartTime,
            EndTime = evt.EndTime,
            RegistrationDeadline = evt.RegistrationDeadline,
            Capacity = evt.Capacity,
            LocationId = evt.Location.Id,
            OrganizerId = evt.Organizer.Id,
            Status = evt.Status
        };

        return View(await BuildEditViewModel(model));
    }

    [HttpPost]
    public async Task<IActionResult> Edit(EditEventViewModel model)
    {
        if (!CanManage) return RedirectToAction("Login", "Auth");

        var result = await _api.PutAsync<EventDetailViewModel>($"/api/events/{model.Id}", new
        {
            model.Title,
            model.Description,
            model.StartTime,
            model.EndTime,
            model.RegistrationDeadline,
            model.Capacity,
            model.LocationId,
            model.OrganizerId
        });

        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Update failed.");
            model = await BuildEditViewModel(model);
            return View(model);
        }

        TempData["Success"] = "Event updated successfully.";
        return RedirectToAction(nameof(Details), new { id = model.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Manage(int id)
    {
        if (!CanManage) return RedirectToAction("Login", "Auth");

        var evt = await _api.GetAsync<EventDetailViewModel>($"/api/events/{id}");
        if (evt is null) return NotFound();
        return View(evt);
    }

    private async Task<CreateEventViewModel> BuildCreateViewModel(CreateEventViewModel model)
    {
        model.Locations = await _api.GetAsync<List<LocationViewModel>>("/api/locations") ?? [];
        model.Organizers = await _api.GetAsync<List<OrganizerViewModel>>("/api/organizers") ?? [];
        if (model.LocationId == 0 && model.Locations.Count > 0)
            model.LocationId = model.Locations[0].Id;
        if (model.OrganizerId == 0 && model.Organizers.Count > 0)
            model.OrganizerId = model.Organizers[0].Id;
        return model;
    }

    private async Task<EditEventViewModel> BuildEditViewModel(EditEventViewModel model)
    {
        var create = await BuildCreateViewModel(model);
        model.Locations = create.Locations;
        model.Organizers = create.Organizers;
        return model;
    }
}
