using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DBMSOnline.Application.DTOs;
using DBMSOnline.Application.Interfaces;

namespace DBMSOnline.Application.Services;

public class SchemaAppService : ISchemaAppService
{
    private readonly IConnectionAppService _connectionAppService;
    private readonly IDbProviderFactory _providerFactory;

    public SchemaAppService(IConnectionAppService connectionAppService, IDbProviderFactory providerFactory)
    {
        _connectionAppService = connectionAppService;
        _providerFactory = providerFactory;
    }

    public async Task<DatabaseSchemaDto> GetDatabasesAsync(Guid connectionId)
    {
        var (conn, pass, cs) = await _connectionAppService.ResolveConnectionCredentialsAsync(connectionId);
        var provider = _providerFactory.GetProvider(conn.DatabaseType);
        var dbs = await provider.GetDatabasesAsync(conn, pass, cs);

        return new DatabaseSchemaDto
        {
            Databases = dbs,
            ActiveDatabase = string.IsNullOrWhiteSpace(conn.DatabaseName) && dbs.Count > 0 ? dbs[0] : conn.DatabaseName
        };
    }

    public async Task<List<TableSchemaDto>> GetTablesAsync(Guid connectionId, string databaseName)
    {
        var (conn, pass, cs) = await _connectionAppService.ResolveConnectionCredentialsAsync(connectionId);
        var provider = _providerFactory.GetProvider(conn.DatabaseType);
        return await provider.GetTablesAsync(conn, databaseName, pass, cs);
    }

    public async Task<List<ColumnSchemaDto>> GetColumnsAsync(Guid connectionId, string databaseName, string tableName)
    {
        var (conn, pass, cs) = await _connectionAppService.ResolveConnectionCredentialsAsync(connectionId);
        var provider = _providerFactory.GetProvider(conn.DatabaseType);
        return await provider.GetColumnsAsync(conn, databaseName, tableName, pass, cs);
    }

    public async Task<List<StoredProcedureDto>> GetStoredProceduresAsync(Guid connectionId, string databaseName)
    {
        var (conn, pass, cs) = await _connectionAppService.ResolveConnectionCredentialsAsync(connectionId);
        var provider = _providerFactory.GetProvider(conn.DatabaseType);
        return await provider.GetStoredProceduresAsync(conn, databaseName, pass, cs);
    }

    public async Task<List<TableRelationDto>> GetRelationsAsync(Guid connectionId, string databaseName)
    {
        var (conn, pass, cs) = await _connectionAppService.ResolveConnectionCredentialsAsync(connectionId);
        var provider = _providerFactory.GetProvider(conn.DatabaseType);
        return await provider.GetRelationsAsync(conn, databaseName, pass, cs);
    }
}
