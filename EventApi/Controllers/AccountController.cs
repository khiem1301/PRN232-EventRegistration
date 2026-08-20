using EventApi.Application.DTOs;
using EventApi.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EventApi.Controllers;

[Authorize]
[Route("api/account")]
public class AccountController : ApiControllerBase
{
    private readonly IAccountService _accountService;

    public AccountController(IAccountService accountService) => _accountService = accountService;

    [HttpGet("me")]
    public async Task<IActionResult> GetMe() =>
        ToActionResult(await _accountService.GetMeAsync(GetCurrentUserId()));

    [HttpPut("me")]
    public async Task<IActionResult> UpdateMe([FromBody] UpdateProfileDto request) =>
        ToActionResult(await _accountService.UpdateMeAsync(GetCurrentUserId(), request));

    [HttpPut("me/password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto request) =>
        ToActionResult(await _accountService.ChangePasswordAsync(GetCurrentUserId(), request));
}
