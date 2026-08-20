using EventApi.Application.DTOs;
using EventApi.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EventApi.Controllers;

[Route("api/organizers")]
public class OrganizersController : ApiControllerBase
{
    private readonly IOrganizerService _organizerService;

    public OrganizersController(IOrganizerService organizerService) => _organizerService = organizerService;

    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> GetAll() =>
        ToActionResult(await _organizerService.GetAllAsync());

    [AllowAnonymous]
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id) =>
        ToActionResult(await _organizerService.GetByIdAsync(id));

    [Authorize(Roles = "Staff,Admin")]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateOrganizerDto request) =>
        ToActionResult(await _organizerService.CreateAsync(request));

    [Authorize(Roles = "Staff,Admin")]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateOrganizerDto request) =>
        ToActionResult(await _organizerService.UpdateAsync(id, request));

    [Authorize(Roles = "Staff,Admin")]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id) =>
        ToActionResult(await _organizerService.DeleteAsync(id));
}
