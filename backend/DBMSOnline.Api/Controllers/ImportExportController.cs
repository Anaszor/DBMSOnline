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

    [HttpPost("export/database")]
    public async Task<IActionResult> ExportDatabase([FromBody] ExportDatabaseRequestDto request)
    {
        var result = await _importExportAppService.ExportDatabaseAsync(request);
        return Ok(result);
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
