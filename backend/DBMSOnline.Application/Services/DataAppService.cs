using System;
using System.Threading.Tasks;
using DBMSOnline.Application.DTOs;
using DBMSOnline.Application.Interfaces;
using DBMSOnline.Domain.Entities;
using DBMSOnline.Domain.Exceptions;
using DBMSOnline.Domain.Interfaces;

namespace DBMSOnline.Application.Services;

public class DataAppService : IDataAppService
{
    private readonly IConnectionAppService _connectionAppService;
    private readonly IDbProviderFactory _providerFactory;
    private readonly IMaskingService _maskingService;
    private readonly IAuditLogger _auditLogger;

    public DataAppService(
        IConnectionAppService connectionAppService,
        IDbProviderFactory providerFactory,
        IMaskingService maskingService,
        IAuditLogger auditLogger)
    {
        _connectionAppService = connectionAppService;
        _providerFactory = providerFactory;
        _maskingService = maskingService;
        _auditLogger = auditLogger;
    }

    public async Task<DataPageResultDto> GetTableDataAsync(DataPageRequestDto request)
    {
        var (conn, pass, cs) = await _connectionAppService.ResolveConnectionCredentialsAsync(request.ConnectionId);
        var provider = _providerFactory.GetProvider(conn.DatabaseType);
        var result = await provider.GetTableDataAsync(conn, request, pass, cs);

        _maskingService.MaskDataPageResult(result);
        return result;
    }

    public async Task<bool> InsertRowAsync(RowMutationRequestDto request)
    {
        SanitizeMutationRequest(request);
        var (conn, pass, cs) = await _connectionAppService.ResolveConnectionCredentialsAsync(request.ConnectionId);
        if (conn.IsReadOnly)
            throw new SecurityViolationException("Row mutations are disabled on Read-Only connections.");

        var provider = _providerFactory.GetProvider(conn.DatabaseType);
        int rows = await provider.InsertRowAsync(conn, request, pass, cs);

        await _auditLogger.LogAsync(new AuditLog
        {
            Action = "INSERT_ROW",
            ConnectionName = conn.Name,
            DatabaseType = conn.DatabaseType,
            TargetDatabase = request.DatabaseName,
            QueryText = $"INSERT INTO {request.TableName}",
            RowsAffected = rows,
            Success = rows > 0
        });

        return rows > 0;
    }

    public async Task<bool> UpdateRowAsync(RowMutationRequestDto request)
    {
        SanitizeMutationRequest(request);
        var (conn, pass, cs) = await _connectionAppService.ResolveConnectionCredentialsAsync(request.ConnectionId);
        if (conn.IsReadOnly)
            throw new SecurityViolationException("Row mutations are disabled on Read-Only connections.");

        var provider = _providerFactory.GetProvider(conn.DatabaseType);
        int rows = await provider.UpdateRowAsync(conn, request, pass, cs);

        await _auditLogger.LogAsync(new AuditLog
        {
            Action = "UPDATE_ROW",
            ConnectionName = conn.Name,
            DatabaseType = conn.DatabaseType,
            TargetDatabase = request.DatabaseName,
            QueryText = $"UPDATE {request.TableName}",
            RowsAffected = rows,
            Success = rows > 0
        });

        return rows > 0;
    }

    public async Task<bool> DeleteRowAsync(RowMutationRequestDto request)
    {
        SanitizeMutationRequest(request);
        var (conn, pass, cs) = await _connectionAppService.ResolveConnectionCredentialsAsync(request.ConnectionId);
        if (conn.IsReadOnly)
            throw new SecurityViolationException("Row mutations are disabled on Read-Only connections.");

        var provider = _providerFactory.GetProvider(conn.DatabaseType);
        int rows = await provider.DeleteRowAsync(conn, request, pass, cs);

        await _auditLogger.LogAsync(new AuditLog
        {
            Action = "DELETE_ROW",
            ConnectionName = conn.Name,
            DatabaseType = conn.DatabaseType,
            TargetDatabase = request.DatabaseName,
            QueryText = $"DELETE FROM {request.TableName}",
            RowsAffected = rows,
            Success = rows > 0
        });

        return rows > 0;
    }

    private static void SanitizeMutationRequest(RowMutationRequestDto request)
    {
        if (request.Data != null)
        {
            var keys = new System.Collections.Generic.List<string>(request.Data.Keys);
            foreach (var key in keys)
            {
                request.Data[key] = UnboxValue(request.Data[key]);
            }
        }

        if (request.PrimaryKey != null)
        {
            var keys = new System.Collections.Generic.List<string>(request.PrimaryKey.Keys);
            foreach (var key in keys)
            {
                request.PrimaryKey[key] = UnboxValue(request.PrimaryKey[key]);
            }
        }
    }

    private static object? UnboxValue(object? value)
    {
        if (value == null) return null;
        if (value is System.Text.Json.JsonElement element)
        {
            switch (element.ValueKind)
            {
                case System.Text.Json.JsonValueKind.Null:
                case System.Text.Json.JsonValueKind.Undefined:
                    return null;
                case System.Text.Json.JsonValueKind.String:
                    string s = element.GetString() ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(s)) return null;
                    if (DateTime.TryParse(s, out var dt)) return dt;
                    return s;
                case System.Text.Json.JsonValueKind.Number:
                    if (element.TryGetInt64(out long lVal)) return lVal;
                    if (element.TryGetDouble(out double dVal)) return dVal;
                    return element.GetRawText();
                case System.Text.Json.JsonValueKind.True:
                    return true;
                case System.Text.Json.JsonValueKind.False:
                    return false;
                default:
                    return element.GetRawText();
            }
        }
        if (value is string str && string.IsNullOrWhiteSpace(str)) return null;
        return value;
    }
}
