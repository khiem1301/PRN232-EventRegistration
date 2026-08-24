using EventRegistration.WebClient.Models;
using EventRegistration.WebClient.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EventRegistration.WebClient.Controllers;

public class LocationsController : Controller
{
    private readonly EventApiClient _api;

    public LocationsController(EventApiClient api) => _api = api;

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var items = await _api.GetAsync<List<LocationViewModel>>("/api/locations") ?? [];
        return View(items);
    }

    [HttpGet]
    [Authorize(Roles = "Staff,Admin")]
    public IActionResult Create() => View(new LocationViewModel());

    [HttpPost]
    [Authorize(Roles = "Staff,Admin")]
    public async Task<IActionResult> Create(LocationViewModel model)
    {
        var result = await _api.PostAsync<LocationViewModel>("/api/locations", new
        {
            model.Name,
            model.Address,
            model.Description,
            model.Capacity
        });

        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Create failed.");
            return View(model);
        }

        TempData["Success"] = "Location created successfully.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    [Authorize(Roles = "Staff,Admin")]
    public async Task<IActionResult> Edit(int id)
    {
        var location = await _api.GetAsync<LocationViewModel>($"/api/locations/{id}");
        if (location is null) return NotFound();
        return View(location);
    }

    [HttpPost]
    [Authorize(Roles = "Staff,Admin")]
    public async Task<IActionResult> Edit(LocationViewModel model)
    {
        var result = await _api.PutAsync<LocationViewModel>($"/api/locations/{model.Id}", new
        {
            model.Name,
            model.Address,
            model.Description,
            model.Capacity,
            model.IsActive
        });

        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Update failed.");
            return View(model);
        }

        TempData["Success"] = "Location updated successfully.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [Authorize(Roles = "Staff,Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await _api.DeleteAsync($"/api/locations/{id}");
        TempData[result.Success ? "Success" : "Error"] = result.Success
            ? "Location deleted successfully."
            : result.Error ?? "Delete failed.";

        return RedirectToAction(nameof(Index));
    }
}
