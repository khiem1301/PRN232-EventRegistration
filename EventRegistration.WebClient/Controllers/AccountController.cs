using System.Security.Claims;
using EventRegistration.WebClient.Models;
using EventRegistration.WebClient.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EventRegistration.WebClient.Controllers;

[Authorize]
public class AccountController : Controller
{
    private readonly EventApiClient _api;

    public AccountController(EventApiClient api) => _api = api;

    [HttpGet]
    public async Task<IActionResult> Index()
    {
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
        var result = await _api.PutAsync<UserViewModel>("/api/account/me", model);
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Update failed.");
            return View(model);
        }

        var user = result.Data!;
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.FullName),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Role, user.Role)
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity));

        TempData["Success"] = "Profile updated successfully.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public IActionResult ChangePassword() => View(new ChangePasswordViewModel());

    [HttpPost]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
    {
        if (model.NewPassword != model.ConfirmPassword)
        {
            ModelState.AddModelError(string.Empty, "Mật khẩu xác nhận không khớp.");
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
