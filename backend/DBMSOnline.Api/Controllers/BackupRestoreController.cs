using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using DBMSOnline.Application.DTOs;
using DBMSOnline.Application.Interfaces;

namespace DBMSOnline.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BackupRestoreController : ControllerBase
{
    private readonly IBackupRestoreAppService _backupRestoreAppService;

    public BackupRestoreController(IBackupRestoreAppService backupRestoreAppService)
    {
        _backupRestoreAppService = backupRestoreAppService;
    }

    [HttpPost("create")]
    public async Task<IActionResult> CreateBackup([FromBody] CreateBackupRequestDto request)
    {
        var result = await _backupRestoreAppService.CreateBackupAsync(request);
        return Ok(result);
    }

    [HttpPost("restore")]
    public async Task<IActionResult> RestoreDatabase([FromBody] RestoreDatabaseRequestDto request)
    {
        var result = await _backupRestoreAppService.RestoreDatabaseAsync(request);
        return Ok(result);
    }

    [HttpGet("history/{connectionId}")]
    public async Task<IActionResult> GetBackupHistory(Guid connectionId)
    {
        var result = await _backupRestoreAppService.GetBackupHistoryAsync(connectionId);
        return Ok(result);
    }
}
