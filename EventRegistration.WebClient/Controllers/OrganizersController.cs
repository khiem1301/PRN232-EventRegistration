using EventRegistration.WebClient.Models;
using EventRegistration.WebClient.Services;
using Microsoft.AspNetCore.Mvc;

namespace EventRegistration.WebClient.Controllers;

public class OrganizersController : Controller
{
    private readonly EventApiClient _api;

    public OrganizersController(EventApiClient api) => _api = api;

    private bool CanManage =>
        HttpContext.Session.GetString("UserRole") is "Staff" or "Admin";

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var items = await _api.GetAsync<List<OrganizerViewModel>>("/api/organizers") ?? [];
        return View(items);
    }

    [HttpGet]
    public IActionResult Create()
    {
        if (!CanManage) return RedirectToAction("Login", "Auth");
        return View(new OrganizerViewModel());
    }

    [HttpPost]
    public async Task<IActionResult> Create(OrganizerViewModel model)
    {
        if (!CanManage) return RedirectToAction("Login", "Auth");

        var result = await _api.PostAsync<OrganizerViewModel>("/api/organizers", new
        {
            model.Name,
            model.ContactEmail,
            model.ContactPhone,
            model.Description
        });

        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Create failed.");
            return View(model);
        }

        TempData["Success"] = "Organizer created successfully.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        if (!CanManage) return RedirectToAction("Login", "Auth");

        var organizer = await _api.GetAsync<OrganizerViewModel>($"/api/organizers/{id}");
        if (organizer is null) return NotFound();
        return View(organizer);
    }

    [HttpPost]
    public async Task<IActionResult> Edit(OrganizerViewModel model)
    {
        if (!CanManage) return RedirectToAction("Login", "Auth");

        var result = await _api.PutAsync<OrganizerViewModel>($"/api/organizers/{model.Id}", new
        {
            model.Name,
            model.ContactEmail,
            model.ContactPhone,
            model.Description,
            model.IsActive
        });

        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Update failed.");
            return View(model);
        }

        TempData["Success"] = "Organizer updated successfully.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        if (!CanManage) return RedirectToAction("Login", "Auth");

        var result = await _api.DeleteAsync($"/api/organizers/{id}");
        TempData[result.Success ? "Success" : "Error"] = result.Success
            ? "Organizer deleted successfully."
            : result.Error ?? "Delete failed.";

        return RedirectToAction(nameof(Index));
    }
}
