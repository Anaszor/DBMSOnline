using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using DBMSOnline.Application.DTOs;
using DBMSOnline.Application.Interfaces;

namespace DBMSOnline.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class QueryController : ControllerBase
{
    private readonly IQueryAppService _queryAppService;

    public QueryController(IQueryAppService queryAppService)
    {
        _queryAppService = queryAppService;
    }

    [HttpPost("execute")]
    public async Task<ActionResult<QueryResultDto>> Execute([FromBody] QueryRequestDto request)
    {
        string clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
        string userRole = HttpContext.User?.Identity?.Name ?? "Admin";

        var result = await _queryAppService.ExecuteQueryAsync(request, clientIp, userRole);
        return Ok(result);
    }
}
