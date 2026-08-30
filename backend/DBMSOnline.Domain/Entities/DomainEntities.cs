using System;
using DBMSOnline.Domain.Enums;

namespace DBMSOnline.Domain.Entities;

public class Tenant
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Domain { get; set; } = string.Empty;
    public string PlanType { get; set; } = "Enterprise"; // Enterprise, Professional, Community
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public UserRole Role { get; set; } = UserRole.Developer;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }
}

public class RolePermission
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public UserRole Role { get; set; }
    public string PermissionKey { get; set; } = string.Empty; // e.g., "CanExecuteWrite", "CanManageConnections", "CanInviteUsers"
    public bool IsAllowed { get; set; } = true;
}

public class SavedConnection
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; } = Guid.Parse("00000000-0000-0000-0000-000000000001");
    public Guid CreatedByUserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public DatabaseType DatabaseType { get; set; }
    public AuthType AuthType { get; set; } = AuthType.BasicPassword;
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; }
    public string DatabaseName { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    
    // Encrypted sensitive connection data (AES-256-GCM Payload)
    public string EncryptedPassword { get; set; } = string.Empty;
    public string EncryptedConnectionString { get; set; } = string.Empty;
    
    public bool IsReadOnly { get; set; } = false;
    public bool EnforceSsl { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class AuditLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; } = Guid.Parse("00000000-0000-0000-0000-000000000001");
    public Guid UserId { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string ClientIp { get; set; } = "127.0.0.1";
    public string UserRole { get; set; } = "Admin";
    public string Action { get; set; } = string.Empty;
    public string ConnectionName { get; set; } = string.Empty;
    public DatabaseType DatabaseType { get; set; }
    public string TargetDatabase { get; set; } = string.Empty;
    public string QueryText { get; set; } = string.Empty;
    public string QueryHash { get; set; } = string.Empty;
    public long ExecutionDurationMs { get; set; }
    public long RowsAffected { get; set; }
    public QueryRiskLevel RiskLevel { get; set; } = QueryRiskLevel.Safe;
    public bool Success { get; set; } = true;
    public string ErrorMessage { get; set; } = string.Empty;
}

public class QueryHistory
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; } = Guid.Parse("00000000-0000-0000-0000-000000000001");
    public Guid ConnectionId { get; set; }
    public string DatabaseName { get; set; } = string.Empty;
    public string QueryText { get; set; } = string.Empty;
    public DateTime ExecutedAt { get; set; } = DateTime.UtcNow;
    public long ExecutionDurationMs { get; set; }
    public long RowsAffected { get; set; }
    public bool Success { get; set; } = true;
    public string ErrorMessage { get; set; } = string.Empty;
}

public class DatabaseBackup
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; } = Guid.Parse("00000000-0000-0000-0000-000000000001");
    public Guid ConnectionId { get; set; }
    public string BackupName { get; set; } = string.Empty;
    public string DatabaseName { get; set; } = string.Empty;
    public DatabaseType DatabaseType { get; set; }
    public long FileSizeBytes { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public string BackupContent { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class SavedReport
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; } = Guid.Parse("00000000-0000-0000-0000-000000000001");
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string QueryText { get; set; } = string.Empty;
    public string WidgetType { get; set; } = "BarChart";
    public string CategoryColumn { get; set; } = string.Empty;
    public string MeasureColumn { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
