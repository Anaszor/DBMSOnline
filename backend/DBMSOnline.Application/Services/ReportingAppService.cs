using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DBMSOnline.Application.DTOs;
using DBMSOnline.Application.Interfaces;
using DBMSOnline.Domain.Entities;
using DBMSOnline.Domain.Exceptions;
using DBMSOnline.Domain.Interfaces;

namespace DBMSOnline.Application.Services;

public class ReportingAppService : IReportingAppService
{
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly IReportRepository _reportRepository;
    private readonly ISavedConnectionRepository _connectionRepository;
    private readonly IConnectionAppService _connectionAppService;
    private readonly IDbProviderFactory _providerFactory;

    public ReportingAppService(
        IAuditLogRepository auditLogRepository,
        IReportRepository reportRepository,
        ISavedConnectionRepository connectionRepository,
        IConnectionAppService connectionAppService,
        IDbProviderFactory providerFactory)
    {
        _auditLogRepository = auditLogRepository;
        _reportRepository = reportRepository;
        _connectionRepository = connectionRepository;
        _connectionAppService = connectionAppService;
        _providerFactory = providerFactory;
    }

    public async Task<TelemetryMetricsDto> GetTelemetryMetricsAsync(Guid? connectionId)
    {
        var logs = await _auditLogRepository.GetAllAsync();
        var conns = await _connectionRepository.GetAllAsync();

        long totalQueries = logs.Count;
        double avgTime = logs.Count > 0 ? logs.Average(l => l.ExecutionDurationMs) : 0;
        double successRate = logs.Count > 0 ? (double)logs.Count(l => l.Success) / logs.Count * 100.0 : 100.0;

        var latencyTrends = logs
            .OrderBy(l => l.Timestamp)
            .TakeLast(20)
            .Select(l => new ChartDataPointDto
            {
                Category = l.Timestamp.ToString("HH:mm:ss"),
                Value = l.ExecutionDurationMs,
                Group = l.ConnectionName
            })
            .ToList();

        var riskLevels = logs
            .GroupBy(l => l.RiskLevel)
            .Select(g => new ChartDataPointDto
            {
                Category = g.Key.ToString(),
                Value = g.Count()
            })
            .ToList();

        var tableDistribution = new List<ChartDataPointDto>();
        int totalTables = 0;

        if (connectionId.HasValue && connectionId.Value != Guid.Empty)
        {
            try
            {
                var (connection, password, cs) = await _connectionAppService.ResolveConnectionCredentialsAsync(connectionId.Value);
                var provider = _providerFactory.GetProvider(connection.DatabaseType);
                var tables = await provider.GetTablesAsync(connection, connection.DatabaseName, password, cs);
                totalTables = tables.Count;

                tableDistribution = tables.Select(t => new ChartDataPointDto
                {
                    Category = t.TableName,
                    Value = t.ApproxRowCount
                }).ToList();
            }
            catch
            {
                // Fallback telemetry if remote DB is unreachable
            }
        }

        return new TelemetryMetricsDto
        {
            TotalQueriesExecuted = totalQueries,
            AverageExecutionTimeMs = Math.Round(avgTime, 2),
            QuerySuccessRatePercentage = Math.Round(successRate, 1),
            ActiveConnectionsCount = conns.Count,
            TotalTablesCount = totalTables,
            LatencyTrends = latencyTrends,
            RiskLevelDistribution = riskLevels,
            TableSizesDistribution = tableDistribution
        };
    }

    public async Task<DashboardAnalyticsDto> GetDashboardAnalyticsAsync(Guid? connectionId)
    {
        var telemetry = await GetTelemetryMetricsAsync(connectionId);
        var reports = await GetSavedReportsAsync();

        return new DashboardAnalyticsDto
        {
            Telemetry = telemetry,
            SavedReports = reports
        };
    }

    public async Task<ReportDto> CreateReportAsync(CreateReportRequestDto request)
    {
        var entity = new SavedReport
        {
            Id = Guid.NewGuid(),
            Title = request.Title,
            Description = request.Description,
            QueryText = request.QueryText,
            WidgetType = request.WidgetType,
            CategoryColumn = request.CategoryColumn,
            MeasureColumn = request.MeasureColumn,
            CreatedAt = DateTime.UtcNow
        };

        await _reportRepository.AddAsync(entity);
        return MapToDto(entity);
    }

    public async Task<ReportDto?> UpdateReportAsync(Guid id, CreateReportRequestDto request)
    {
        var existing = await _reportRepository.GetByIdAsync(id);
        if (existing == null) return null;

        existing.Title = request.Title;
        existing.Description = request.Description;
        existing.QueryText = request.QueryText;
        existing.WidgetType = request.WidgetType;
        existing.CategoryColumn = request.CategoryColumn;
        existing.MeasureColumn = request.MeasureColumn;

        await _reportRepository.UpdateAsync(existing);
        return MapToDto(existing);
    }

    public async Task<bool> DeleteReportAsync(Guid id)
    {
        var existing = await _reportRepository.GetByIdAsync(id);
        if (existing == null) return false;

        await _reportRepository.DeleteAsync(existing);
        return true;
    }

    public async Task<List<ReportDto>> GetSavedReportsAsync()
    {
        var reports = await _reportRepository.GetAllAsync();
        if (reports.Count == 0)
        {
            var defaultReport = new SavedReport
            {
                Id = Guid.NewGuid(),
                Title = "Audit Query Latency Analytics",
                Description = "Visualizes execution duration (ms) across recent database operations.",
                QueryText = "SELECT Action AS Category, ExecutionDurationMs AS Value FROM AuditLogs ORDER BY Timestamp DESC LIMIT 20;",
                WidgetType = "BarChart",
                CategoryColumn = "Category",
                MeasureColumn = "Value",
                CreatedAt = DateTime.UtcNow
            };
            await _reportRepository.AddAsync(defaultReport);
            reports.Add(defaultReport);
        }

        return reports.Select(MapToDto).ToList();
    }

    public async Task<QueryResultDto> ExecuteReportQueryAsync(Guid reportId, Guid connectionId, string databaseName)
    {
        var report = await _reportRepository.GetByIdAsync(reportId) 
                     ?? throw new DomainException("Report template not found.");

        var (connection, password, cs) = await _connectionAppService.ResolveConnectionCredentialsAsync(connectionId);
        var provider = _providerFactory.GetProvider(connection.DatabaseType);

        string dbName = string.IsNullOrEmpty(databaseName) ? connection.DatabaseName : databaseName;
        return await provider.ExecuteQueryAsync(connection, dbName, report.QueryText, 1000, 30, password, cs);
    }

    private static ReportDto MapToDto(SavedReport r) => new()
    {
        Id = r.Id,
        Title = r.Title,
        Description = r.Description,
        QueryText = r.QueryText,
        WidgetType = r.WidgetType,
        CategoryColumn = r.CategoryColumn,
        MeasureColumn = r.MeasureColumn,
        CreatedAt = r.CreatedAt
    };
}
