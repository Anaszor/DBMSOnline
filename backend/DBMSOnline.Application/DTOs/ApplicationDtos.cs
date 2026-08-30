using System;
using System.Collections.Generic;
using DBMSOnline.Domain.Enums;

namespace DBMSOnline.Application.DTOs;

public class ConnectionRequestDto
{
    public Guid? Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DatabaseType DatabaseType { get; set; }
    public AuthType AuthType { get; set; } = AuthType.BasicPassword;
    public string Host { get; set; } = "localhost";
    public int Port { get; set; }
    public string DatabaseName { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string CustomConnectionString { get; set; } = string.Empty;
    public bool IsReadOnly { get; set; } = false;
    public bool EnforceSsl { get; set; } = true;
}

public class ConnectionResponseDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DatabaseType DatabaseType { get; set; }
    public AuthType AuthType { get; set; }
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; }
    public string DatabaseName { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string CustomConnectionString { get; set; } = string.Empty;
    public bool IsReadOnly { get; set; }
    public bool EnforceSsl { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class DatabaseSchemaDto
{
    public List<string> Databases { get; set; } = new();
    public string ActiveDatabase { get; set; } = string.Empty;
}

public class TableSchemaDto
{
    public string TableName { get; set; } = string.Empty;
    public string SchemaName { get; set; } = string.Empty;
    public string TableType { get; set; } = "TABLE"; // TABLE, VIEW, COLLECTION
    public long ApproxRowCount { get; set; }
}

public class ColumnSchemaDto
{
    public string ColumnName { get; set; } = string.Empty;
    public string DataType { get; set; } = string.Empty;
    public bool IsNullable { get; set; }
    public bool IsPrimaryKey { get; set; }
    public bool IsForeignKey { get; set; }
    public bool IsAutoIncrement { get; set; }
    public string DefaultValue { get; set; } = string.Empty;
    public int MaxLength { get; set; }
}

public class ProcedureParameterDto
{
    public string ParameterName { get; set; } = string.Empty;
    public string DataType { get; set; } = string.Empty;
    public string ParameterMode { get; set; } = "IN";
}

public class StoredProcedureDto
{
    public string ProcedureName { get; set; } = string.Empty;
    public string SchemaName { get; set; } = string.Empty;
    public string RoutineType { get; set; } = "PROCEDURE"; // PROCEDURE or FUNCTION
    public string Definition { get; set; } = string.Empty;
    public List<ProcedureParameterDto> Parameters { get; set; } = new();
}

public class TableRelationDto
{
    public string PKTable { get; set; } = string.Empty;
    public string PKColumn { get; set; } = string.Empty;
    public string FKTable { get; set; } = string.Empty;
    public string FKColumn { get; set; } = string.Empty;
    public string ConstraintName { get; set; } = string.Empty;
}

public class QueryRequestDto
{
    public Guid ConnectionId { get; set; }
    public string DatabaseName { get; set; } = string.Empty;
    public string QueryText { get; set; } = string.Empty;
    public int MaxRows { get; set; } = 1000;
    public int TimeoutSeconds { get; set; } = 30;
}

public class QueryResultDto
{
    public List<string> Columns { get; set; } = new();
    public List<Dictionary<string, object?>> Rows { get; set; } = new();
    public long RowsAffected { get; set; }
    public long ExecutionDurationMs { get; set; }
    public QueryRiskLevel RiskLevel { get; set; }
    public string WarningMessage { get; set; } = string.Empty;
    public bool IsTruncated { get; set; }
    public bool Success { get; set; } = true;
    public string ErrorMessage { get; set; } = string.Empty;
}

public class DataPageRequestDto
{
    public Guid ConnectionId { get; set; }
    public string DatabaseName { get; set; } = string.Empty;
    public string TableName { get; set; } = string.Empty;
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
    public string? SortColumn { get; set; }
    public bool SortAscending { get; set; } = true;
    public string? FilterColumn { get; set; }
    public string? FilterValue { get; set; }
}

public class DataPageResultDto
{
    public List<ColumnSchemaDto> Columns { get; set; } = new();
    public List<Dictionary<string, object?>> Rows { get; set; } = new();
    public long TotalRows { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalRows / PageSize);
}

public class RowMutationRequestDto
{
    public Guid ConnectionId { get; set; }
    public string DatabaseName { get; set; } = string.Empty;
    public string TableName { get; set; } = string.Empty;
    public Dictionary<string, object?> PrimaryKey { get; set; } = new();
    public Dictionary<string, object?> Data { get; set; } = new();
}

public class UserDto
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public string RoleName => Role.ToString();
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class TenantDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Domain { get; set; } = string.Empty;
    public string PlanType { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public int UserCount { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class LoginRequestDto
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class LoginResponseDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
    public UserDto? User { get; set; }
    public TenantDto? Tenant { get; set; }
}

public class CreateUserRequestDto
{
    public Guid TenantId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public UserRole Role { get; set; } = UserRole.Developer;
}

public class CreateTenantRequestDto
{
    public string Name { get; set; } = string.Empty;
    public string Domain { get; set; } = string.Empty;
    public string PlanType { get; set; } = "Enterprise";
    public string AdminEmail { get; set; } = string.Empty;
    public string AdminPassword { get; set; } = string.Empty;
    public string AdminFullName { get; set; } = string.Empty;
}

// --- Import / Export DTOs ---
public class ExportTableRequestDto
{
    public Guid ConnectionId { get; set; }
    public string DatabaseName { get; set; } = string.Empty;
    public string TableName { get; set; } = string.Empty;
    public string Format { get; set; } = "sql"; // sql, csv, json
    public bool IncludeData { get; set; } = true;
    public bool IncludeSchema { get; set; } = true;
}

public class ExportDatabaseRequestDto
{
    public Guid ConnectionId { get; set; }
    public string DatabaseName { get; set; } = string.Empty;
    public string Format { get; set; } = "sql"; // sql, json
}

public class ExportResultDto
{
    public bool Success { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class ImportTableRequestDto
{
    public Guid ConnectionId { get; set; }
    public string DatabaseName { get; set; } = string.Empty;
    public string TableName { get; set; } = string.Empty;
    public string FileFormat { get; set; } = "csv"; // csv, json
    public string FileContent { get; set; } = string.Empty;
    public bool CreateTableIfNotExists { get; set; } = true;
}

public class ImportDatabaseRequestDto
{
    public Guid ConnectionId { get; set; }
    public string DatabaseName { get; set; } = string.Empty;
    public string SqlScript { get; set; } = string.Empty;
}

public class ImportResultDto
{
    public bool Success { get; set; }
    public int RowsImported { get; set; }
    public int StatementsExecuted { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<string> Errors { get; set; } = new();
}

// --- BI Reporting & Telemetry DTOs ---
public class ChartDataPointDto
{
    public string Category { get; set; } = string.Empty;
    public double Value { get; set; }
    public string Group { get; set; } = string.Empty;
}

public class TelemetryMetricsDto
{
    public long TotalQueriesExecuted { get; set; }
    public double AverageExecutionTimeMs { get; set; }
    public double QuerySuccessRatePercentage { get; set; }
    public int ActiveConnectionsCount { get; set; }
    public int TotalTablesCount { get; set; }
    public List<ChartDataPointDto> LatencyTrends { get; set; } = new();
    public List<ChartDataPointDto> RiskLevelDistribution { get; set; } = new();
    public List<ChartDataPointDto> TableSizesDistribution { get; set; } = new();
}

public class DashboardAnalyticsDto
{
    public TelemetryMetricsDto Telemetry { get; set; } = new();
    public List<ReportDto> SavedReports { get; set; } = new();
}

public class ReportDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string QueryText { get; set; } = string.Empty;
    public string WidgetType { get; set; } = "BarChart"; // BarChart, LineChart, PieChart, KpiCard, DataTable
    public string CategoryColumn { get; set; } = string.Empty;
    public string MeasureColumn { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class CreateReportRequestDto
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string QueryText { get; set; } = string.Empty;
    public string WidgetType { get; set; } = "BarChart";
    public string CategoryColumn { get; set; } = string.Empty;
    public string MeasureColumn { get; set; } = string.Empty;
}

// --- Database Backup & Restore DTOs ---
public class CreateBackupRequestDto
{
    public Guid ConnectionId { get; set; }
    public string DatabaseName { get; set; } = string.Empty;
    public string BackupName { get; set; } = string.Empty;
}

public class RestoreDatabaseRequestDto
{
    public Guid ConnectionId { get; set; }
    public string TargetDatabaseName { get; set; } = string.Empty;
    public string BackupContentOrPath { get; set; } = string.Empty;
}

public class DatabaseBackupDto
{
    public Guid Id { get; set; }
    public Guid ConnectionId { get; set; }
    public string BackupName { get; set; } = string.Empty;
    public string DatabaseName { get; set; } = string.Empty;
    public DatabaseType DatabaseType { get; set; }
    public long FileSizeBytes { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class BackupResultDto
{
    public bool Success { get; set; }
    public DatabaseBackupDto? Backup { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class RestoreResultDto
{
    public bool Success { get; set; }
    public int StatementsExecuted { get; set; }
    public string Message { get; set; } = string.Empty;
}
