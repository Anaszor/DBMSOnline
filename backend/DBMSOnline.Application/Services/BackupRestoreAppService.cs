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

public class BackupRestoreAppService : IBackupRestoreAppService
{
    private readonly IDatabaseBackupRepository _backupRepository;
    private readonly IConnectionAppService _connectionAppService;
    private readonly IImportExportAppService _importExportAppService;

    public BackupRestoreAppService(
        IDatabaseBackupRepository backupRepository,
        IConnectionAppService connectionAppService,
        IImportExportAppService importExportAppService)
    {
        _backupRepository = backupRepository;
        _connectionAppService = connectionAppService;
        _importExportAppService = importExportAppService;
    }

    public async Task<BackupResultDto> CreateBackupAsync(CreateBackupRequestDto request)
    {
        var (connection, password, cs) = await _connectionAppService.ResolveConnectionCredentialsAsync(request.ConnectionId);
        string dbName = string.IsNullOrEmpty(request.DatabaseName) ? connection.DatabaseName : request.DatabaseName;

        // Perform Database Export Snapshot
        var exportResult = await _importExportAppService.ExportDatabaseAsync(new ExportDatabaseRequestDto
        {
            ConnectionId = request.ConnectionId,
            DatabaseName = dbName,
            Format = "sql"
        });

        string backupName = string.IsNullOrWhiteSpace(request.BackupName)
            ? $"{dbName}_Backup_{DateTime.UtcNow:yyyyMMdd_HHmmss}"
            : request.BackupName;

        var entity = new DatabaseBackup
        {
            Id = Guid.NewGuid(),
            TenantId = connection.TenantId,
            ConnectionId = request.ConnectionId,
            BackupName = backupName,
            DatabaseName = dbName,
            DatabaseType = connection.DatabaseType,
            FileSizeBytes = exportResult.FileSizeBytes,
            FilePath = exportResult.FileName,
            BackupContent = exportResult.Content,
            CreatedAt = DateTime.UtcNow
        };

        await _backupRepository.AddAsync(entity);

        var dto = MapToDto(entity);
        return new BackupResultDto
        {
            Success = true,
            Backup = dto,
            BackupContent = entity.BackupContent,
            Message = $"Full database backup '{backupName}' created successfully."
        };
    }

    public async Task<RestoreResultDto> RestoreDatabaseAsync(RestoreDatabaseRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.BackupContentOrPath))
            throw new DomainException("Backup content for restore cannot be empty.");

        var importResult = await _importExportAppService.ImportDatabaseScriptAsync(new ImportDatabaseRequestDto
        {
            ConnectionId = request.ConnectionId,
            DatabaseName = request.TargetDatabaseName,
            SqlScript = request.BackupContentOrPath
        });

        return new RestoreResultDto
        {
            Success = importResult.Success,
            StatementsExecuted = importResult.StatementsExecuted,
            Message = importResult.Message
        };
    }

    public async Task<List<DatabaseBackupDto>> GetBackupHistoryAsync(Guid connectionId)
    {
        var backups = await _backupRepository.GetByConnectionIdAsync(connectionId);
        return backups.Select(MapToDto).ToList();
    }

    public async Task<DatabaseBackupDto?> GetBackupByIdAsync(Guid id)
    {
        var backup = await _backupRepository.GetByIdAsync(id);
        return backup != null ? MapToDto(backup) : null;
    }

    public async Task<(byte[] contentBytes, string fileName, string contentType)?> DownloadBackupAsync(Guid id)
    {
        var backup = await _backupRepository.GetByIdAsync(id);
        if (backup == null) return null;

        byte[] bytes;
        if (!string.IsNullOrEmpty(backup.BackupContent))
        {
            bytes = System.Text.Encoding.UTF8.GetBytes(backup.BackupContent);
        }
        else if (!string.IsNullOrEmpty(backup.FilePath) && System.IO.File.Exists(backup.FilePath))
        {
            bytes = await System.IO.File.ReadAllBytesAsync(backup.FilePath);
        }
        else
        {
            bytes = Array.Empty<byte>();
        }

        string fileName = !string.IsNullOrEmpty(backup.BackupName)
            ? (backup.BackupName.EndsWith(".sql", StringComparison.OrdinalIgnoreCase) ? backup.BackupName : $"{backup.BackupName}.sql")
            : $"{backup.DatabaseName}_Backup.sql";

        return (bytes, fileName, "application/sql");
    }

    private static DatabaseBackupDto MapToDto(DatabaseBackup b) => new()
    {
        Id = b.Id,
        ConnectionId = b.ConnectionId,
        BackupName = b.BackupName,
        DatabaseName = b.DatabaseName,
        DatabaseType = b.DatabaseType,
        FileSizeBytes = b.FileSizeBytes,
        FilePath = b.FilePath,
        BackupContent = b.BackupContent,
        CreatedAt = b.CreatedAt
    };
}
