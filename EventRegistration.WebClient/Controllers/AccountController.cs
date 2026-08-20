using EventRegistration.WebClient.Models;
using EventRegistration.WebClient.Services;
using Microsoft.AspNetCore.Mvc;

namespace EventRegistration.WebClient.Controllers;

public class AccountController : Controller
{
    private readonly EventApiClient _api;

    public AccountController(EventApiClient api) => _api = api;

    private bool IsLoggedIn => !string.IsNullOrEmpty(HttpContext.Session.GetString("JwtToken"));

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        if (!IsLoggedIn) return RedirectToAction("Login", "Auth");

        var profile = await _api.GetAsync<UserViewModel>("/api/account/me");
        if (profile is null) return RedirectToAction("Login", "Auth");

        return View(new UpdateProfileViewModel
        {
            FullName = profile.FullName,
            StudentCode = profile.StudentCode,
            Phone = profile.Phone
        });
    }

    [HttpPost]
    public async Task<IActionResult> Index(UpdateProfileViewModel model)
    {
        if (!IsLoggedIn) return RedirectToAction("Login", "Auth");

        var result = await _api.PutAsync<UserViewModel>("/api/account/me", model);
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Update failed.");
            return View(model);
        }

        HttpContext.Session.SetString("UserName", result.Data!.FullName);
        TempData["Success"] = "Profile updated successfully.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public IActionResult ChangePassword()
    {
        if (!IsLoggedIn) return RedirectToAction("Login", "Auth");
        return View(new ChangePasswordViewModel());
    }

    [HttpPost]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
    {
        if (!IsLoggedIn) return RedirectToAction("Login", "Auth");

        if (model.NewPassword != model.ConfirmPassword)
        {
            ModelState.AddModelError(nameof(model.ConfirmPassword), "Passwords do not match.");
            return View(model);
        }

        var result = await _api.PutAsync<object>("/api/account/me/password", new
        {
            model.CurrentPassword,
            model.NewPassword
        });

        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Password change failed.");
            return View(model);
        }

        TempData["Success"] = "Password changed successfully.";
        return RedirectToAction(nameof(Index));
    }
}
