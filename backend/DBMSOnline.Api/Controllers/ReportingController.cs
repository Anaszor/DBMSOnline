using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using DBMSOnline.Application.DTOs;
using DBMSOnline.Application.Interfaces;

namespace DBMSOnline.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ReportingController : ControllerBase
{
    private readonly IReportingAppService _reportingAppService;

    public ReportingController(IReportingAppService reportingAppService)
    {
        _reportingAppService = reportingAppService;
    }

    [HttpGet("telemetry")]
    public async Task<IActionResult> GetTelemetry([FromQuery] Guid? connectionId)
    {
        var result = await _reportingAppService.GetTelemetryMetricsAsync(connectionId);
        return Ok(result);
    }

    [HttpGet("analytics")]
    public async Task<IActionResult> GetDashboardAnalytics([FromQuery] Guid? connectionId)
    {
        var result = await _reportingAppService.GetDashboardAnalyticsAsync(connectionId);
        return Ok(result);
    }

    [HttpGet("reports")]
    public async Task<IActionResult> GetSavedReports()
    {
        var result = await _reportingAppService.GetSavedReportsAsync();
        return Ok(result);
    }

    [HttpPost("reports")]
    public async Task<IActionResult> CreateReport([FromBody] CreateReportRequestDto request)
    {
        var result = await _reportingAppService.CreateReportAsync(request);
        return Ok(result);
    }

    [HttpPut("reports/{id}")]
    public async Task<IActionResult> UpdateReport(Guid id, [FromBody] CreateReportRequestDto request)
    {
        var result = await _reportingAppService.UpdateReportAsync(id, request);
        if (result == null) return NotFound();
        return Ok(result);
    }

    [HttpDelete("reports/{id}")]
    public async Task<IActionResult> DeleteReport(Guid id)
    {
        var success = await _reportingAppService.DeleteReportAsync(id);
        if (!success) return NotFound();
        return Ok(new { success = true });
    }

    [HttpPost("reports/{reportId}/execute")]
    public async Task<IActionResult> ExecuteReportQuery(Guid reportId, [FromQuery] Guid connectionId, [FromQuery] string databaseName)
    {
        var result = await _reportingAppService.ExecuteReportQueryAsync(reportId, connectionId, databaseName);
        return Ok(result);
    }
}
