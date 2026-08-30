using System;
using System.Threading.Tasks;
using DBMSOnline.Application.DTOs;
using DBMSOnline.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace DBMSOnline.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthAppService _authAppService;

    public AuthController(IAuthAppService authAppService)
    {
        _authAppService = authAppService;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto request)
    {
        var res = await _authAppService.LoginAsync(request);
        if (!res.Success)
        {
            return Unauthorized(res);
        }
        return Ok(res);
    }

    [HttpPost("users")]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserRequestDto request)
    {
        var user = await _authAppService.CreateUserAsync(request);
        return Ok(user);
    }

    [HttpGet("tenants/{tenantId}/users")]
    public async Task<IActionResult> GetTenantUsers(Guid tenantId)
    {
        var users = await _authAppService.GetTenantUsersAsync(tenantId);
        return Ok(users);
    }

    [HttpPost("tenants")]
    public async Task<IActionResult> CreateTenant([FromBody] CreateTenantRequestDto request)
    {
        var tenant = await _authAppService.CreateTenantAsync(request);
        return Ok(tenant);
    }

    [HttpGet("tenants")]
    public async Task<IActionResult> GetAllTenants()
    {
        var tenants = await _authAppService.GetAllTenantsAsync();
        return Ok(tenants);
    }
}
