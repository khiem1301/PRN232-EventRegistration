using EventRegistration.WebClient.Models;
using EventRegistration.WebClient.Services;
using Microsoft.AspNetCore.Mvc;

namespace EventRegistration.WebClient.Controllers;

public class UsersController : Controller
{
    private readonly EventApiClient _api;

    public UsersController(EventApiClient api) => _api = api;

    private bool IsAdmin => HttpContext.Session.GetString("UserRole") == "Admin";

    [HttpGet]
    public async Task<IActionResult> Index(string? search, int page = 1)
    {
        if (!IsAdmin) return RedirectToAction("Login", "Auth");

        var url = $"/api/users?search={Uri.EscapeDataString(search ?? "")}&page={page}&pageSize=10";
        var data = await _api.GetAsync<PagedUsersViewModel>(url);

        var model = data ?? new PagedUsersViewModel { Page = page, PageSize = 10, Search = search };
        return View(model);
    }

    [HttpGet]
    public IActionResult Create()
    {
        if (!IsAdmin) return RedirectToAction("Login", "Auth");
        return View(new CreateUserViewModel());
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateUserViewModel model)
    {
        if (!IsAdmin) return RedirectToAction("Login", "Auth");

        var result = await _api.PostAsync<UserViewModel>("/api/users", model);
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Create failed.");
            return View(model);
        }

        TempData["Success"] = "User created successfully.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        if (!IsAdmin) return RedirectToAction("Login", "Auth");

        var user = await _api.GetAsync<UserViewModel>($"/api/users/{id}");
        if (user is null) return NotFound();

        return View(new EditUserViewModel
        {
            Id = user.Id,
            Email = user.Email,
            FullName = user.FullName,
            StudentCode = user.StudentCode,
            Phone = user.Phone,
            Role = user.Role,
            IsActive = user.IsActive
        });
    }

    [HttpPost]
    public async Task<IActionResult> Edit(EditUserViewModel model)
    {
        if (!IsAdmin) return RedirectToAction("Login", "Auth");

        var result = await _api.PutAsync<UserViewModel>($"/api/users/{model.Id}", new
        {
            model.FullName,
            model.StudentCode,
            model.Phone,
            model.Role,
            model.IsActive
        });

        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Update failed.");
            return View(model);
        }

        TempData["Success"] = "User updated successfully.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Deactivate(int id)
    {
        if (!IsAdmin) return RedirectToAction("Login", "Auth");

        var result = await _api.DeleteAsync($"/api/users/{id}");
        TempData[result.Success ? "Success" : "Error"] = result.Success
            ? "User deactivated successfully."
            : result.Error ?? "Deactivate failed.";

        return RedirectToAction(nameof(Index));
    }
}
