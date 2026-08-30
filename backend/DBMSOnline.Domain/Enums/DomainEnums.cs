namespace DBMSOnline.Domain.Enums;

public enum DatabaseType
{
    SqlServer = 1,
    PostgreSql = 2,
    MySql = 3,
    Sqlite = 4,
    MongoDb = 5,
    Oracle = 6
}

public enum AuthType
{
    BasicPassword = 1,
    WindowsAuth = 2,
    ConnectionString = 3,
    MongoDbScram = 4
}

public enum UserRole
{
    OwnerSuperAdmin = 1, // Platform Owner (You) - Global Governance & Org Provisioning
    OrgAdmin = 2,        // Tenant / Team Admin - Team & Access Management
    DBA = 3,             // Database Administrator - Full Schema & DB Control
    Developer = 4,       // Data Developer / Analyst - Query & DML Access
    Auditor = 5          // Read-Only Security Auditor
}

public enum QueryRiskLevel
{
    Safe = 0,
    Low = 1,
    Moderate = 2,
    High = 3,
    Critical = 4
}
