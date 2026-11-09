using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DBMSOnline.Application.DTOs;
using DBMSOnline.Domain.Entities;
using DBMSOnline.Domain.Enums;

namespace DBMSOnline.Application.Interfaces;

public interface IDbProvider
{
    DatabaseType DatabaseType { get; }
    
    Task<bool> TestConnectionAsync(SavedConnection connection, string decryptedPassword, string decryptedConnectionString);
    Task<List<string>> GetDatabasesAsync(SavedConnection connection, string decryptedPassword, string decryptedConnectionString);
    Task<List<TableSchemaDto>> GetTablesAsync(SavedConnection connection, string databaseName, string decryptedPassword, string decryptedConnectionString);
    Task<List<ColumnSchemaDto>> GetColumnsAsync(SavedConnection connection, string databaseName, string tableName, string decryptedPassword, string decryptedConnectionString);
    Task<List<StoredProcedureDto>> GetStoredProceduresAsync(SavedConnection connection, string databaseName, string decryptedPassword, string decryptedConnectionString);
    Task<List<TableRelationDto>> GetRelationsAsync(SavedConnection connection, string databaseName, string decryptedPassword, string decryptedConnectionString);
    Task<QueryResultDto> ExecuteQueryAsync(SavedConnection connection, string databaseName, string queryText, int maxRows, int timeoutSeconds, string decryptedPassword, string decryptedConnectionString);
    Task<DataPageResultDto> GetTableDataAsync(SavedConnection connection, DataPageRequestDto request, string decryptedPassword, string decryptedConnectionString);
    Task<int> InsertRowAsync(SavedConnection connection, RowMutationRequestDto request, string decryptedPassword, string decryptedConnectionString);
    Task<int> UpdateRowAsync(SavedConnection connection, RowMutationRequestDto request, string decryptedPassword, string decryptedConnectionString);
    Task<int> DeleteRowAsync(SavedConnection connection, RowMutationRequestDto request, string decryptedPassword, string decryptedConnectionString);
}

public interface IDbProviderFactory
{
    IDbProvider GetProvider(DatabaseType dbType);
}

public interface IConnectionAppService
{
    Task<ConnectionResponseDto> SaveConnectionAsync(ConnectionRequestDto request);
    Task<List<ConnectionResponseDto>> GetAllConnectionsAsync();
    Task<ConnectionResponseDto?> GetConnectionByIdAsync(Guid id);
    Task<bool> DeleteConnectionAsync(Guid id);
    Task<bool> TestConnectionAsync(ConnectionRequestDto request);
    Task<(SavedConnection connection, string password, string connectionString)> ResolveConnectionCredentialsAsync(Guid id);
}

public interface ISchemaAppService
{
    Task<DatabaseSchemaDto> GetDatabasesAsync(Guid connectionId);
    Task<List<TableSchemaDto>> GetTablesAsync(Guid connectionId, string databaseName);
    Task<List<ColumnSchemaDto>> GetColumnsAsync(Guid connectionId, string databaseName, string tableName);
    Task<List<StoredProcedureDto>> GetStoredProceduresAsync(Guid connectionId, string databaseName);
    Task<List<TableRelationDto>> GetRelationsAsync(Guid connectionId, string databaseName);
}

public interface IQueryAppService
{
    Task<QueryResultDto> ExecuteQueryAsync(QueryRequestDto request, string clientIp, string userRole);
}

public interface IDataAppService
{
    Task<DataPageResultDto> GetTableDataAsync(DataPageRequestDto request);
    Task<bool> InsertRowAsync(RowMutationRequestDto request);
    Task<bool> UpdateRowAsync(RowMutationRequestDto request);
    Task<bool> DeleteRowAsync(RowMutationRequestDto request);
}

public interface IAuthAppService
{
    Task<LoginResponseDto> LoginAsync(LoginRequestDto request);
    Task<UserDto> CreateUserAsync(CreateUserRequestDto request);
    Task<TenantDto> CreateTenantAsync(CreateTenantRequestDto request);
    Task<List<UserDto>> GetTenantUsersAsync(Guid tenantId);
    Task<List<TenantDto>> GetAllTenantsAsync();
}

public interface IMaskingService
{
    void MaskQueryResult(QueryResultDto result);
    void MaskDataPageResult(DataPageResultDto result);
}

public interface IImportExportAppService
{
    Task<ExportResultDto> ExportTableAsync(ExportTableRequestDto request);
    Task<ExportResultDto> ExportDatabaseAsync(ExportDatabaseRequestDto request);
    Task<ImportResultDto> ImportTableDataAsync(ImportTableRequestDto request);
    Task<ImportResultDto> ImportDatabaseScriptAsync(ImportDatabaseRequestDto request);
}

public interface IReportingAppService
{
    Task<TelemetryMetricsDto> GetTelemetryMetricsAsync(Guid? connectionId);
    Task<DashboardAnalyticsDto> GetDashboardAnalyticsAsync(Guid? connectionId);
    Task<ReportDto> CreateReportAsync(CreateReportRequestDto request);
    Task<ReportDto?> UpdateReportAsync(Guid id, CreateReportRequestDto request);
    Task<bool> DeleteReportAsync(Guid id);
    Task<List<ReportDto>> GetSavedReportsAsync();
    Task<QueryResultDto> ExecuteReportQueryAsync(Guid reportId, Guid connectionId, string databaseName);
}

public interface IBackupRestoreAppService
{
    Task<BackupResultDto> CreateBackupAsync(CreateBackupRequestDto request);
    Task<RestoreResultDto> RestoreDatabaseAsync(RestoreDatabaseRequestDto request);
    Task<List<DatabaseBackupDto>> GetBackupHistoryAsync(Guid connectionId);
    Task<DatabaseBackupDto?> GetBackupByIdAsync(Guid id);
    Task<(byte[] contentBytes, string fileName, string contentType)?> DownloadBackupAsync(Guid id);
}

public interface IGeminiCopilotService
{
    Task<CopilotFixResponseDto> AutoFixQueryAsync(CopilotFixRequestDto request);
    Task<CopilotGenerateResponseDto> GenerateQueryAsync(CopilotGenerateRequestDto request);
}
