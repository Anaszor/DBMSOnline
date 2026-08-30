using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using DBMSOnline.Application.DTOs;
using DBMSOnline.Application.Interfaces;

namespace DBMSOnline.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ConnectionsController : ControllerBase
{
    private readonly IConnectionAppService _connectionAppService;

    public ConnectionsController(IConnectionAppService connectionAppService)
    {
        _connectionAppService = connectionAppService;
    }

    [HttpGet]
    public async Task<ActionResult<List<ConnectionResponseDto>>> GetAll()
    {
        var result = await _connectionAppService.GetAllConnectionsAsync();
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ConnectionResponseDto>> GetById(Guid id)
    {
        var result = await _connectionAppService.GetConnectionByIdAsync(id);
        if (result == null) return NotFound();
        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<ConnectionResponseDto>> Save([FromBody] ConnectionRequestDto request)
    {
        var result = await _connectionAppService.SaveConnectionAsync(request);
        return Ok(result);
    }

    [HttpPost("test")]
    public async Task<ActionResult<bool>> Test([FromBody] ConnectionRequestDto request)
    {
        bool success = await _connectionAppService.TestConnectionAsync(request);
        return Ok(new { success, message = success ? "Connection successful!" : "Failed to connect to database." });
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<bool>> Delete(Guid id)
    {
        bool deleted = await _connectionAppService.DeleteConnectionAsync(id);
        if (!deleted) return NotFound();
        return Ok(true);
    }
}
