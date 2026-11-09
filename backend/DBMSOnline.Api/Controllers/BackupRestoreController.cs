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

    [HttpGet("download/{id}")]
    public async Task<IActionResult> DownloadBackup(Guid id)
    {
        var result = await _backupRestoreAppService.DownloadBackupAsync(id);
        if (result == null) return NotFound("Backup file record not found.");

        return File(result.Value.contentBytes, result.Value.contentType, result.Value.fileName);
    }
}
