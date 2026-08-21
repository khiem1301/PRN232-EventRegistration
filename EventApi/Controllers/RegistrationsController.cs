using EventApi.Application.DTOs;
using EventApi.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EventApi.Controllers;

[Route("api/registrations")]
public class RegistrationsController : ApiControllerBase
{
    private readonly IRegistrationService _registrationService;

    public RegistrationsController(IRegistrationService registrationService) => _registrationService = registrationService;

    [Authorize]
    [HttpGet("me")]
    [HttpGet("my")]
    public async Task<IActionResult> GetMyRegistrations() =>
        ToActionResult(await _registrationService.GetMyRegistrationsAsync(GetCurrentUserId()));

    [Authorize(Roles = "Staff,Admin")]
    [HttpGet("events/{eventId:int}")]
    [HttpGet("event/{eventId:int}")]
    public async Task<IActionResult> GetEventRegistrations(int eventId) =>
        ToActionResult(await _registrationService.GetEventRegistrationsAsync(eventId, GetCurrentUserRole()));

    [Authorize(Roles = "Staff,Admin")]
    [HttpPost("event/{eventId:int}/mark-noshow")]
    public async Task<IActionResult> MarkNoShow(int eventId) =>
        ToActionResult(await _registrationService.MarkNoShowAsync(eventId, GetCurrentUserRole() ?? "Student"));

    [Authorize]
    [HttpPost]
    public async Task<IActionResult> Register([FromBody] RegisterEventDto request) =>
        ToActionResult(await _registrationService.RegisterAsync(GetCurrentUserId(), request));

    [Authorize]
    [HttpPost("{id:int}/cancel")]
    public async Task<IActionResult> Cancel(int id) =>
        ToActionResult(await _registrationService.CancelAsync(id, GetCurrentUserId(), GetCurrentUserRole()));

    [Authorize(Roles = "Staff,Admin")]
    [HttpPost("{id:int}/checkin")]
    [HttpPost("{id:int}/check-in")]
    public async Task<IActionResult> CheckIn(int id, [FromBody] CheckInRegistrationDto request) =>
        ToActionResult(await _registrationService.CheckInAsync(id, GetCurrentUserRole() ?? "Student", request));

    private string? GetCurrentUserRole() =>
        User.Identity?.IsAuthenticated == true
            ? User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value
                ?? User.FindFirst("role")?.Value
            : null;
}
