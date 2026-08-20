using EventApi.Application.DTOs;
using EventApi.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EventApi.Controllers;

[Route("api/locations")]
public class LocationsController : ApiControllerBase
{
    private readonly ILocationService _locationService;

    public LocationsController(ILocationService locationService) => _locationService = locationService;

    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> GetAll() =>
        ToActionResult(await _locationService.GetAllAsync());

    [AllowAnonymous]
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id) =>
        ToActionResult(await _locationService.GetByIdAsync(id));

    [Authorize(Roles = "Staff,Admin")]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateLocationDto request) =>
        ToActionResult(await _locationService.CreateAsync(request));

    [Authorize(Roles = "Staff,Admin")]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateLocationDto request) =>
        ToActionResult(await _locationService.UpdateAsync(id, request));

    [Authorize(Roles = "Staff,Admin")]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id) =>
        ToActionResult(await _locationService.DeleteAsync(id));
}
