using EventRegistration.WebClient.Models;
using EventRegistration.WebClient.Services;
using Microsoft.AspNetCore.Mvc;

namespace EventRegistration.WebClient.Controllers;

public class FeedbacksController : Controller
{
    private readonly EventApiClient _api;

    public FeedbacksController(EventApiClient api) => _api = api;

    private bool IsLoggedIn => !string.IsNullOrEmpty(HttpContext.Session.GetString("JwtToken"));

    [HttpGet]
    public async Task<IActionResult> MyFeedbacks()
    {
        if (!IsLoggedIn) return RedirectToAction("Login", "Auth");

        var feedbacks = await _api.GetAsync<List<FeedbackViewModel>>("/api/feedbacks/me");
        return View(feedbacks ?? new List<FeedbackViewModel>());
    }

    [HttpGet]
    public IActionResult Create(int eventId, string eventTitle)
    {
        if (!IsLoggedIn) return RedirectToAction("Login", "Auth");

        return View(new CreateFeedbackViewModel
        {
            EventId = eventId,
            EventTitle = eventTitle
        });
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateFeedbackViewModel model)
    {
        if (!IsLoggedIn) return RedirectToAction("Login", "Auth");

        if (model.Rating < 1 || model.Rating > 5)
        {
            ModelState.AddModelError(nameof(model.Rating), "Rating must be between 1 and 5.");
            return View(model);
        }

        var result = await _api.PostAsync<FeedbackViewModel>("/api/feedbacks", new
        {
            model.EventId,
            model.Rating,
            model.Comment
        });

        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Submit feedback failed.");
            return View(model);
        }

        TempData["Success"] = "Feedback submitted successfully.";
        return RedirectToAction(nameof(MyFeedbacks));
    }
}
