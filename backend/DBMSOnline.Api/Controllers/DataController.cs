using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using DBMSOnline.Application.DTOs;
using DBMSOnline.Application.Interfaces;

namespace DBMSOnline.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DataController : ControllerBase
{
    private readonly IDataAppService _dataAppService;

    public DataController(IDataAppService dataAppService)
    {
        _dataAppService = dataAppService;
    }

    [HttpPost("page")]
    public async Task<ActionResult<DataPageResultDto>> GetPage([FromBody] DataPageRequestDto request)
    {
        var result = await _dataAppService.GetTableDataAsync(request);
        return Ok(result);
    }

    [HttpPost("insert")]
    public async Task<ActionResult<bool>> InsertRow([FromBody] RowMutationRequestDto request)
    {
        bool success = await _dataAppService.InsertRowAsync(request);
        return Ok(success);
    }

    [HttpPut("update")]
    public async Task<ActionResult<bool>> UpdateRow([FromBody] RowMutationRequestDto request)
    {
        bool success = await _dataAppService.UpdateRowAsync(request);
        return Ok(success);
    }

    [HttpPost("delete")]
    public async Task<ActionResult<bool>> DeleteRow([FromBody] RowMutationRequestDto request)
    {
        bool success = await _dataAppService.DeleteRowAsync(request);
        return Ok(success);
    }
}
