using EventRegistration.WebClient.Models;
using EventRegistration.WebClient.Services;
using Microsoft.AspNetCore.Mvc;

namespace EventRegistration.WebClient.Controllers;

public class LocationsController : Controller
{
    private readonly EventApiClient _api;

    public LocationsController(EventApiClient api) => _api = api;

    private bool CanManage =>
        HttpContext.Session.GetString("UserRole") is "Staff" or "Admin";

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var items = await _api.GetAsync<List<LocationViewModel>>("/api/locations") ?? [];
        return View(items);
    }

    [HttpGet]
    public IActionResult Create()
    {
        if (!CanManage) return RedirectToAction("Login", "Auth");
        return View(new LocationViewModel());
    }

    [HttpPost]
    public async Task<IActionResult> Create(LocationViewModel model)
    {
        if (!CanManage) return RedirectToAction("Login", "Auth");

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
    public async Task<IActionResult> Edit(int id)
    {
        if (!CanManage) return RedirectToAction("Login", "Auth");

        var location = await _api.GetAsync<LocationViewModel>($"/api/locations/{id}");
        if (location is null) return NotFound();
        return View(location);
    }

    [HttpPost]
    public async Task<IActionResult> Edit(LocationViewModel model)
    {
        if (!CanManage) return RedirectToAction("Login", "Auth");

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
    public async Task<IActionResult> Delete(int id)
    {
        if (!CanManage) return RedirectToAction("Login", "Auth");

        var result = await _api.DeleteAsync($"/api/locations/{id}");
        TempData[result.Success ? "Success" : "Error"] = result.Success
            ? "Location deleted successfully."
            : result.Error ?? "Delete failed.";

        return RedirectToAction(nameof(Index));
    }
}
