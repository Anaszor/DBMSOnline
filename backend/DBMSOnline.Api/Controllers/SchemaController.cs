using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using DBMSOnline.Application.DTOs;
using DBMSOnline.Application.Interfaces;

namespace DBMSOnline.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SchemaController : ControllerBase
{
    private readonly ISchemaAppService _schemaAppService;

    public SchemaController(ISchemaAppService schemaAppService)
    {
        _schemaAppService = schemaAppService;
    }

    [HttpGet("databases/{connectionId:guid}")]
    public async Task<ActionResult<DatabaseSchemaDto>> GetDatabases(Guid connectionId)
    {
        var result = await _schemaAppService.GetDatabasesAsync(connectionId);
        return Ok(result);
    }

    [HttpGet("tables/{connectionId:guid}")]
    public async Task<ActionResult<List<TableSchemaDto>>> GetTables(Guid connectionId, [FromQuery] string? databaseName)
    {
        var result = await _schemaAppService.GetTablesAsync(connectionId, databaseName ?? string.Empty);
        return Ok(result);
    }

    [HttpGet("columns/{connectionId:guid}")]
    public async Task<ActionResult<List<ColumnSchemaDto>>> GetColumns(Guid connectionId, [FromQuery] string databaseName, [FromQuery] string tableName)
    {
        var result = await _schemaAppService.GetColumnsAsync(connectionId, databaseName, tableName);
        return Ok(result);
    }

    [HttpGet("stored-procedures/{connectionId:guid}")]
    public async Task<ActionResult<List<StoredProcedureDto>>> GetStoredProcedures(Guid connectionId, [FromQuery] string databaseName)
    {
        var result = await _schemaAppService.GetStoredProceduresAsync(connectionId, databaseName);
        return Ok(result);
    }

    [HttpGet("relations/{connectionId:guid}")]
    public async Task<ActionResult<List<TableRelationDto>>> GetRelations(Guid connectionId, [FromQuery] string databaseName)
    {
        var result = await _schemaAppService.GetRelationsAsync(connectionId, databaseName);
        return Ok(result);
    }
}
