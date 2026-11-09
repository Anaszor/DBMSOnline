using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using DBMSOnline.Application.DTOs;
using DBMSOnline.Application.Interfaces;

namespace DBMSOnline.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CopilotController : ControllerBase
{
    private readonly IGeminiCopilotService _copilotService;

    public CopilotController(IGeminiCopilotService copilotService)
    {
        _copilotService = copilotService;
    }

    [HttpPost("autofix")]
    public async Task<ActionResult<CopilotFixResponseDto>> AutoFix([FromBody] CopilotFixRequestDto request)
    {
        var result = await _copilotService.AutoFixQueryAsync(request);
        return Ok(result);
    }

    [HttpPost("generate")]
    public async Task<ActionResult<CopilotGenerateResponseDto>> Generate([FromBody] CopilotGenerateRequestDto request)
    {
        var result = await _copilotService.GenerateQueryAsync(request);
        return Ok(result);
    }
}
