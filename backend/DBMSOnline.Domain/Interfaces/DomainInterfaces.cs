using System.Threading.Tasks;
using DBMSOnline.Domain.Entities;
using DBMSOnline.Domain.Enums;

namespace DBMSOnline.Domain.Interfaces;

public interface IEncryptionService
{
    string Encrypt(string plainText);
    string Decrypt(string cipherText);
    byte[] HashMasterKey(string masterKey, byte[] salt);
}

public interface ISsrfGuard
{
    void ValidateHost(string host);
}

public interface IQuerySafetyService
{
    (QueryRiskLevel riskLevel, string warningMessage, bool isAllowed) AnalyzeQuery(string query, DatabaseType dbType, bool isReadOnlyConnection);
    string SanitizeConnectionString(string connectionString, DatabaseType dbType);
}

public interface IAuditLogger
{
    Task LogAsync(AuditLog log);
}

public interface IUserRepository
{
    Task<User?> GetByEmailAsync(string email);
    Task<List<User>> GetByTenantIdAsync(Guid tenantId);
    Task AddAsync(User user);
    Task UpdateAsync(User user);
}

public interface ITenantRepository
{
    Task<Tenant?> GetByIdAsync(Guid id);
    Task<List<Tenant>> GetAllAsync();
    Task AddAsync(Tenant tenant);
}

public interface IAuditLogRepository
{
    Task<List<AuditLog>> GetAllAsync();
    Task AddAsync(AuditLog log);
}

public interface IReportRepository
{
    Task<List<SavedReport>> GetAllAsync();
    Task<SavedReport?> GetByIdAsync(Guid id);
    Task AddAsync(SavedReport report);
    Task UpdateAsync(SavedReport report);
    Task DeleteAsync(SavedReport report);
}

public interface IDatabaseBackupRepository
{
    Task<List<DatabaseBackup>> GetByConnectionIdAsync(Guid connectionId);
    Task<DatabaseBackup?> GetByIdAsync(Guid id);
    Task AddAsync(DatabaseBackup backup);
}
