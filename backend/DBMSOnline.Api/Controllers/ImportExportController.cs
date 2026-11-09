using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using DBMSOnline.Application.DTOs;
using DBMSOnline.Application.Interfaces;

namespace DBMSOnline.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ImportExportController : ControllerBase
{
    private readonly IImportExportAppService _importExportAppService;

    public ImportExportController(IImportExportAppService importExportAppService)
    {
        _importExportAppService = importExportAppService;
    }

    [HttpPost("export/table")]
    public async Task<IActionResult> ExportTable([FromBody] ExportTableRequestDto request)
    {
        var result = await _importExportAppService.ExportTableAsync(request);
        return Ok(result);
    }

    [HttpPost("export/table/download")]
    public async Task<IActionResult> DownloadExportTable([FromBody] ExportTableRequestDto request)
    {
        var result = await _importExportAppService.ExportTableAsync(request);
        var bytes = System.Text.Encoding.UTF8.GetBytes(result.Content ?? string.Empty);
        return File(bytes, result.ContentType ?? "application/octet-stream", result.FileName ?? "export.sql");
    }

    [HttpGet("export/table/download")]
    public async Task<IActionResult> DownloadExportTableGet([FromQuery] System.Guid connectionId, [FromQuery] string databaseName, [FromQuery] string tableName, [FromQuery] string format = "sql")
    {
        var result = await _importExportAppService.ExportTableAsync(new ExportTableRequestDto
        {
            ConnectionId = connectionId,
            DatabaseName = databaseName,
            TableName = tableName,
            Format = format,
            IncludeData = true,
            IncludeSchema = true
        });
        var bytes = System.Text.Encoding.UTF8.GetBytes(result.Content ?? string.Empty);
        return File(bytes, result.ContentType ?? "application/octet-stream", result.FileName ?? $"{tableName}.{format}");
    }

    [HttpPost("export/database")]
    public async Task<IActionResult> ExportDatabase([FromBody] ExportDatabaseRequestDto request)
    {
        var result = await _importExportAppService.ExportDatabaseAsync(request);
        return Ok(result);
    }

    [HttpPost("export/database/download")]
    public async Task<IActionResult> DownloadExportDatabase([FromBody] ExportDatabaseRequestDto request)
    {
        var result = await _importExportAppService.ExportDatabaseAsync(request);
        var bytes = System.Text.Encoding.UTF8.GetBytes(result.Content ?? string.Empty);
        return File(bytes, result.ContentType ?? "application/sql", result.FileName ?? "db_export.sql");
    }

    [HttpGet("export/database/download")]
    public async Task<IActionResult> DownloadExportDatabaseGet([FromQuery] System.Guid connectionId, [FromQuery] string databaseName, [FromQuery] string format = "sql")
    {
        var result = await _importExportAppService.ExportDatabaseAsync(new ExportDatabaseRequestDto
        {
            ConnectionId = connectionId,
            DatabaseName = databaseName,
            Format = format
        });
        var bytes = System.Text.Encoding.UTF8.GetBytes(result.Content ?? string.Empty);
        return File(bytes, result.ContentType ?? "application/sql", result.FileName ?? $"{databaseName}_Export.sql");
    }

    [HttpPost("import/table")]
    public async Task<IActionResult> ImportTable([FromBody] ImportTableRequestDto request)
    {
        var result = await _importExportAppService.ImportTableDataAsync(request);
        return Ok(result);
    }

    [HttpPost("import/script")]
    public async Task<IActionResult> ImportScript([FromBody] ImportDatabaseRequestDto request)
    {
        var result = await _importExportAppService.ImportDatabaseScriptAsync(request);
        return Ok(result);
    }
}
