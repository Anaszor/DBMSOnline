using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using DBMSOnline.Application.DTOs;
using DBMSOnline.Application.Interfaces;
using DBMSOnline.Domain.Entities;
using DBMSOnline.Domain.Enums;
using DBMSOnline.Domain.Exceptions;
using DBMSOnline.Domain.Interfaces;

namespace DBMSOnline.Application.Services;

public class QueryAppService : IQueryAppService
{
    private readonly IConnectionAppService _connectionAppService;
    private readonly IDbProviderFactory _providerFactory;
    private readonly IQuerySafetyService _querySafetyService;
    private readonly IMaskingService _maskingService;
    private readonly IAuditLogger _auditLogger;

    public QueryAppService(
        IConnectionAppService connectionAppService,
        IDbProviderFactory providerFactory,
        IQuerySafetyService querySafetyService,
        IMaskingService maskingService,
        IAuditLogger auditLogger)
    {
        _connectionAppService = connectionAppService;
        _providerFactory = providerFactory;
        _querySafetyService = querySafetyService;
        _maskingService = maskingService;
        _auditLogger = auditLogger;
    }

    public async Task<QueryResultDto> ExecuteQueryAsync(QueryRequestDto request, string clientIp, string userRole)
    {
        var (conn, pass, cs) = await _connectionAppService.ResolveConnectionCredentialsAsync(request.ConnectionId);

        // 1. Analyze query safety & risk level
        var (riskLevel, warningMsg, isAllowed) = _querySafetyService.AnalyzeQuery(request.QueryText, conn.DatabaseType, conn.IsReadOnly);

        if (!isAllowed)
        {
            await _auditLogger.LogAsync(new AuditLog
            {
                ClientIp = clientIp,
                UserRole = userRole,
                Action = "EXECUTE_QUERY_BLOCKED",
                ConnectionName = conn.Name,
                DatabaseType = conn.DatabaseType,
                TargetDatabase = request.DatabaseName,
                QueryText = request.QueryText,
                RiskLevel = riskLevel,
                Success = false,
                ErrorMessage = warningMsg
            });

            throw new SecurityViolationException($"Query execution blocked by security engine: {warningMsg}", warningMsg);
        }

        // 2. Execute Query via Provider
        var provider = _providerFactory.GetProvider(conn.DatabaseType);
        var result = await provider.ExecuteQueryAsync(conn, request.DatabaseName, request.QueryText, request.MaxRows, request.TimeoutSeconds, pass, cs);

        result.RiskLevel = riskLevel;
        result.WarningMessage = warningMsg;

        // 3. Mask Sensitive Fields
        _maskingService.MaskQueryResult(result);

        // 4. Audit Log
        string queryHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(request.QueryText)));
        await _auditLogger.LogAsync(new AuditLog
        {
            ClientIp = clientIp,
            UserRole = userRole,
            Action = "EXECUTE_QUERY",
            ConnectionName = conn.Name,
            DatabaseType = conn.DatabaseType,
            TargetDatabase = request.DatabaseName,
            QueryText = request.QueryText,
            QueryHash = queryHash,
            ExecutionDurationMs = result.ExecutionDurationMs,
            RowsAffected = result.RowsAffected,
            RiskLevel = riskLevel,
            Success = result.Success,
            ErrorMessage = result.ErrorMessage
        });

        return result;
    }
}
