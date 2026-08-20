using EventRegistration.WebClient.Models;
using EventRegistration.WebClient.Services;
using Microsoft.AspNetCore.Mvc;

namespace EventRegistration.WebClient.Controllers;

public class AuthController : Controller
{
    private readonly EventApiClient _api;

    public AuthController(EventApiClient api) => _api = api;

    [HttpGet]
    public IActionResult Login() => View(new LoginViewModel());

    [HttpPost]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        var result = await _api.PostAsync<LoginResponseViewModel>("/api/auth/login", model);
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Login failed.");
            return View(model);
        }

        HttpContext.Session.SetString("JwtToken", result.Data!.Token);
        HttpContext.Session.SetString("UserName", result.Data.User.FullName);
        HttpContext.Session.SetString("UserRole", result.Data.User.Role);

        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    public IActionResult Register() => View(new RegisterViewModel());

    [HttpPost]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (model.Password != model.ConfirmPassword)
        {
            ModelState.AddModelError(nameof(model.ConfirmPassword), "Passwords do not match.");
            return View(model);
        }

        var result = await _api.PostAsync<UserViewModel>("/api/auth/register", new
        {
            model.Email,
            model.Password,
            model.FullName,
            model.StudentCode,
            model.Phone
        });

        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Registration failed.");
            return View(model);
        }

        TempData["Success"] = "Registration successful. Please login.";
        return RedirectToAction(nameof(Login));
    }

    public IActionResult Logout()
    {
        HttpContext.Session.Clear();
        return RedirectToAction(nameof(Login));
    }
}
