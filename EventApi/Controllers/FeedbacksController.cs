using EventApi.Application.DTOs;
using EventApi.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EventApi.Controllers;

[Route("api/feedbacks")]
public class FeedbacksController : ApiControllerBase
{
    private readonly IFeedbackService _feedbackService;

    public FeedbacksController(IFeedbackService feedbackService) => _feedbackService = feedbackService;

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> GetMyFeedbacks() =>
        ToActionResult(await _feedbackService.GetMyFeedbacksAsync(GetCurrentUserId()));

    [AllowAnonymous]
    [HttpGet("event/{eventId:int}")]
    public async Task<IActionResult> GetEventFeedbacks(int eventId) =>
        ToActionResult(await _feedbackService.GetEventFeedbacksAsync(eventId));

    [Authorize]
    [HttpPost]
    public async Task<IActionResult> Submit([FromBody] CreateFeedbackDto request) =>
        ToActionResult(await _feedbackService.SubmitAsync(GetCurrentUserId(), request));
}
