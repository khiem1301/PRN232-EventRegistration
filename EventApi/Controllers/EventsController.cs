using EventApi.Application.DTOs;
using EventApi.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EventApi.Controllers;

[Route("api/events")]
public class EventsController : ApiControllerBase
{
    private readonly IEventService _eventService;

    public EventsController(IEventService eventService) => _eventService = eventService;

    private string? GetCurrentUserRole() =>
        User.Identity?.IsAuthenticated == true
            ? User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value
                ?? User.FindFirst("role")?.Value
            : null;

    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] EventQueryParams query) =>
        ToActionResult(await _eventService.GetAllAsync(query, GetCurrentUserRole()));

    [AllowAnonymous]
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id) =>
        ToActionResult(await _eventService.GetByIdAsync(id, GetCurrentUserRole()));

    [Authorize(Roles = "Staff,Admin")]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateEventDto request) =>
        ToActionResult(await _eventService.CreateAsync(request, GetCurrentUserId()));

    [Authorize(Roles = "Staff,Admin")]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateEventDto request) =>
        ToActionResult(await _eventService.UpdateAsync(id, request, GetCurrentUserRole() ?? "Staff"));
}
