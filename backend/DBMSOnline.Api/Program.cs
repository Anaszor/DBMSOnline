using System;
using System.IO;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using FluentValidation;
using DBMSOnline.Api.Middleware;
using DBMSOnline.Application.Interfaces;
using DBMSOnline.Application.Services;
using DBMSOnline.Application.Validators;
using DBMSOnline.Domain.Entities;
using DBMSOnline.Domain.Enums;
using DBMSOnline.Domain.Interfaces;
using DBMSOnline.Infrastructure.DbProviders;
using DBMSOnline.Infrastructure.Persistence;
using DBMSOnline.Infrastructure.Security;

var builder = WebApplication.CreateBuilder(args);

// 1. Database Context (SQLite Metadata DB)
string dbPath = Path.Combine(AppContext.BaseDirectory, "dbms_metadata.db");
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite($"Data Source={dbPath}"));

builder.Services.AddScoped<DbContext>(sp => sp.GetRequiredService<AppDbContext>());

// 2. Register Security & Persistence Services
builder.Services.AddSingleton<IEncryptionService>(new AesGcmEncryptionService("DBMSOnline_Enterprise_Master_Key_2026!"));
builder.Services.AddSingleton<ISsrfGuard>(new SsrfGuardService(allowLocalhost: true));
builder.Services.AddSingleton<IQuerySafetyService, QuerySafetyEngine>();
builder.Services.AddScoped<ISavedConnectionRepository, SavedConnectionRepository>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<ITenantRepository, TenantRepository>();
builder.Services.AddScoped<IAuditLogRepository, AuditLogRepository>();
builder.Services.AddScoped<IReportRepository, ReportRepository>();
builder.Services.AddScoped<IDatabaseBackupRepository, DatabaseBackupRepository>();
builder.Services.AddScoped<IAuditLogger, AuditLogger>();
builder.Services.AddSingleton<IMaskingService, MaskingService>();

// 3. Register DB Providers Strategy Pattern
builder.Services.AddScoped<IDbProvider, SqliteProvider>();
builder.Services.AddScoped<IDbProvider, SqlServerProvider>();
builder.Services.AddScoped<IDbProvider, PostgreSqlProvider>();
builder.Services.AddScoped<IDbProvider, MySqlProvider>();
builder.Services.AddScoped<IDbProvider, MongoDbProvider>();
builder.Services.AddScoped<IDbProvider, OracleProvider>();
builder.Services.AddScoped<IDbProviderFactory, DbProviderFactory>();

// 4. Register Application Services
builder.Services.AddScoped<IConnectionAppService, ConnectionAppService>();
builder.Services.AddScoped<ISchemaAppService, SchemaAppService>();
builder.Services.AddScoped<IQueryAppService, QueryAppService>();
builder.Services.AddScoped<IDataAppService, DataAppService>();
builder.Services.AddScoped<IAuthAppService, AuthAppService>();
builder.Services.AddScoped<IImportExportAppService, ImportExportAppService>();
builder.Services.AddScoped<IReportingAppService, ReportingAppService>();
builder.Services.AddScoped<IBackupRestoreAppService, BackupRestoreAppService>();
builder.Services.AddHttpClient();
builder.Services.AddScoped<IGeminiCopilotService, GeminiCopilotService>();

// 4b. Register SignalR Hubs
builder.Services.AddSignalR();

// 5. Register FluentValidation
builder.Services.AddValidatorsFromAssemblyContaining<ConnectionRequestDtoValidator>();

// 6. Controllers & OpenAPI / Swagger
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.MaxDepth = 256;
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
    });

builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
{
    options.ValueLengthLimit = int.MaxValue;
    options.MultipartBodyLengthLimit = long.MaxValue;
    options.MemoryBufferThreshold = int.MaxValue;
    options.MultipartHeadersLengthLimit = int.MaxValue;
});

builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.Limits.MaxRequestBodySize = null; // Unlimited request body size for large DB dumps
    serverOptions.Limits.MaxResponseBufferSize = null; // Unlimited response buffer size to fix download buffer limits
    serverOptions.Limits.MinResponseDataRate = null; // Disable minimum data rate limit for large DB exports/downloads
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "DBMSOnline Enterprise API",
        Version = "v1",
        Description = "ASP.NET Core 9 Clean Onion Architecture Database Management Platform Web API"
    });
});

// 7. CORS Policy
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngularSpa", policy =>
    {
        policy.WithOrigins("http://localhost:4200", "https://localhost:4200")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// 8. Rate Limiting Policy
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = 429;
    options.AddFixedWindowLimiter("StrictQueryLimit", limitOptions =>
    {
        limitOptions.PermitLimit = 60;
        limitOptions.Window = TimeSpan.FromMinutes(1);
        limitOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        limitOptions.QueueLimit = 10;
    });
});

var app = builder.Build();

// 9. Initialize Metadata DB & Demo SQLite Database
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    dbContext.Database.EnsureCreated();

    // Create rich enterprise sample demo SQLite database file if not exists
    string demoDbPath = Path.Combine(AppContext.BaseDirectory, "demo_dbms.db");
    using (var conn = new SqliteConnection($"Data Source={demoDbPath}"))
    {
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS Departments (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL,
                Location TEXT
            );

            CREATE TABLE IF NOT EXISTS Employees (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                FullName TEXT NOT NULL,
                Position TEXT,
                Department TEXT,
                Salary REAL,
                Email TEXT,
                Password TEXT
            );

            CREATE TABLE IF NOT EXISTS Categories (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL,
                Description TEXT
            );

            CREATE TABLE IF NOT EXISTS Products (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL,
                Category TEXT NOT NULL,
                Price REAL NOT NULL,
                StockQuantity INTEGER NOT NULL
            );

            CREATE TABLE IF NOT EXISTS Orders (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                CustomerName TEXT NOT NULL,
                TotalAmount REAL NOT NULL,
                Status TEXT NOT NULL,
                OrderDate TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS Customers (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL,
                Company TEXT,
                City TEXT,
                Country TEXT
            );

            INSERT OR IGNORE INTO Departments (Id, Name, Location) VALUES 
            (1, 'Engineering', 'Building A - Silicon Valley'),
            (2, 'Finance', 'Building B - New York'),
            (3, 'Operations', 'Building C - Chicago'),
            (4, 'Marketing', 'Building D - London'),
            (5, 'Sales', 'Building E - Tokyo');

            INSERT OR IGNORE INTO Employees (Id, FullName, Position, Department, Salary, Email, Password) VALUES 
            (1, 'Alex Morgan', 'Principal Lead Engineer', 'Engineering', 145000, 'alex.m@cloudenterprise.io', 'Pass123!'),
            (2, 'Samantha Reed', 'Senior Financial Analyst', 'Finance', 98000, 'samantha.r@cloudenterprise.io', 'SecretKey456'),
            (3, 'David Chen', 'Cloud Architect', 'Engineering', 135000, 'david.c@cloudenterprise.io', 'EncryptedPass789'),
            (4, 'Elena Rostova', 'VP of Marketing', 'Marketing', 120000, 'elena.r@cloudenterprise.io', 'MktPass999'),
            (5, 'Marcus Vance', 'Enterprise Sales Director', 'Sales', 115000, 'marcus.v@cloudenterprise.io', 'SalesPass2026');

            INSERT OR IGNORE INTO Categories (Id, Name, Description) VALUES
            (1, 'Cloud Compute', 'Server instances and GPU clusters'),
            (2, 'Storage & DB', 'Managed object storage and relational DBs'),
            (3, 'Security & IAM', 'Zero trust enterprise access tools'),
            (4, 'Networking', 'VPC, CDN and edge routing services');

            INSERT OR IGNORE INTO Products (Id, Name, Category, Price, StockQuantity) VALUES
            (1, 'Titan Compute Node 64vCPU', 'Cloud Compute', 420.00, 150),
            (2, 'HyperEdge AI Accelerator', 'Cloud Compute', 890.00, 75),
            (3, 'NVMe Ultra High-IOPS Tier', 'Storage & DB', 120.00, 500),
            (4, 'Quantum-Safe Shield Proxy', 'Security & IAM', 250.00, 320),
            (5, 'Global Anycast Edge CDN', 'Networking', 95.00, 800),
            (6, 'Enterprise PostgreSQL Cluster', 'Storage & DB', 310.00, 210);

            INSERT OR IGNORE INTO Orders (Id, CustomerName, TotalAmount, Status, OrderDate) VALUES
            (1, 'Acme Global Corp', 4200.00, 'Completed', '2026-10-15'),
            (2, 'CyberNet Solutions', 1890.00, 'Completed', '2026-10-18'),
            (3, 'Apex Financial Group', 5600.00, 'Processing', '2026-10-22'),
            (4, 'Nova Logistics Ltd', 1250.00, 'Shipped', '2026-10-24'),
            (5, 'Starlight Media Tech', 3100.00, 'Completed', '2026-11-01');

            INSERT OR IGNORE INTO Customers (Id, Name, Company, City, Country) VALUES
            (1, 'Sarah Jenkins', 'Acme Global Corp', 'San Francisco', 'United States'),
            (2, 'Tariq Al-Mansoor', 'CyberNet Solutions', 'Dubai', 'UAE'),
            (3, 'Oliver Schmidt', 'Apex Financial Group', 'Frankfurt', 'Germany'),
            (4, 'Mei-Ling Zhou', 'Nova Logistics Ltd', 'Singapore', 'Singapore'),
            (5, 'Liam O''Connor', 'Starlight Media Tech', 'Dublin', 'Ireland');
        ";
        cmd.ExecuteNonQuery();
    }

    // Seed default Demo Connection & Cloud Connections if no connections exist
    if (!dbContext.SavedConnections.Any())
    {
        var enc = scope.ServiceProvider.GetRequiredService<IEncryptionService>();

        // 1. AWS RDS PostgreSQL Cloud
        dbContext.SavedConnections.Add(new SavedConnection
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.Parse("00000000-0000-0000-0000-000000000001"),
            CreatedByUserId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Name = "AWS RDS PostgreSQL Cloud",
            DatabaseType = DatabaseType.PostgreSql,
            AuthType = AuthType.BasicPassword,
            Host = "rds-postgres-prod.us-east-1.amazonaws.com",
            Port = 5432,
            DatabaseName = "retail_production_db",
            Username = "db_admin",
            EncryptedPassword = enc.Encrypt("DemoCloudSecret2026!"),
            IsReadOnly = false,
            EnforceSsl = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        // 2. Azure SQL Server (MSSQL Enterprise)
        dbContext.SavedConnections.Add(new SavedConnection
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.Parse("00000000-0000-0000-0000-000000000001"),
            CreatedByUserId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Name = "Azure SQL Server (MSSQL)",
            DatabaseType = DatabaseType.SqlServer,
            AuthType = AuthType.BasicPassword,
            Host = "azure-sql-cluster.database.windows.net",
            Port = 1433,
            DatabaseName = "enterprise_dw",
            Username = "cloud_admin",
            EncryptedPassword = enc.Encrypt("AzureMssqlPass2026!"),
            IsReadOnly = false,
            EnforceSsl = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        // 3. Oracle Cloud Database (OCI)
        dbContext.SavedConnections.Add(new SavedConnection
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.Parse("00000000-0000-0000-0000-000000000001"),
            CreatedByUserId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Name = "Oracle Cloud Database (OCI)",
            DatabaseType = DatabaseType.Oracle,
            AuthType = AuthType.BasicPassword,
            Host = "oci-db.us-ashburn-1.oraclecloud.com",
            Port = 1521,
            DatabaseName = "sales_autonomous",
            Username = "admin",
            EncryptedPassword = enc.Encrypt("OracleSecure2026!"),
            IsReadOnly = false,
            EnforceSsl = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        // 4. Google Cloud SQL (MySQL)
        dbContext.SavedConnections.Add(new SavedConnection
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.Parse("00000000-0000-0000-0000-000000000001"),
            CreatedByUserId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Name = "Google Cloud SQL (MySQL)",
            DatabaseType = DatabaseType.MySql,
            AuthType = AuthType.BasicPassword,
            Host = "gcp-mysql-prod.internal",
            Port = 3306,
            DatabaseName = "commerce_mysql",
            Username = "app_user",
            EncryptedPassword = enc.Encrypt("GcpMySqlPass2026!"),
            IsReadOnly = false,
            EnforceSsl = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        // 5. StoreHub Enterprise Demo DB (SQLite)
        dbContext.SavedConnections.Add(new SavedConnection
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.Parse("00000000-0000-0000-0000-000000000001"),
            CreatedByUserId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Name = "StoreHub Enterprise Demo DB",
            DatabaseType = DatabaseType.Sqlite,
            AuthType = AuthType.BasicPassword,
            Host = "localhost",
            DatabaseName = demoDbPath,
            Username = "admin",
            IsReadOnly = false,
            EnforceSsl = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        // Seed Clean Enterprise Audit Logs
        dbContext.AuditLogs.AddRange(
            new AuditLog
            {
                Id = Guid.NewGuid(),
                TenantId = Guid.Parse("00000000-0000-0000-0000-000000000001"),
                Timestamp = DateTime.UtcNow.AddMinutes(-5),
                ClientIp = "192.168.1.104",
                UserRole = "ADMIN",
                Action = "EXECUTE_QUERY",
                ConnectionName = "AWS RDS PostgreSQL Cloud",
                DatabaseType = DatabaseType.PostgreSql,
                TargetDatabase = "retail_production_db",
                QueryText = "SELECT * FROM \"Products\" WHERE \"Category\" = 'Cloud Compute' LIMIT 50;",
                QueryHash = "H7F29A4D",
                ExecutionDurationMs = 2,
                RowsAffected = 24,
                RiskLevel = QueryRiskLevel.Safe,
                Success = true
            },
            new AuditLog
            {
                Id = Guid.NewGuid(),
                TenantId = Guid.Parse("00000000-0000-0000-0000-000000000001"),
                Timestamp = DateTime.UtcNow.AddMinutes(-12),
                ClientIp = "192.168.1.104",
                UserRole = "ADMIN",
                Action = "EXECUTE_QUERY",
                ConnectionName = "StoreHub Enterprise Demo DB",
                DatabaseType = DatabaseType.Sqlite,
                TargetDatabase = "StoreHub_Demo_DB",
                QueryText = "SELECT Department, SUM(Salary) AS TotalBudget FROM Employees GROUP BY Department;",
                QueryHash = "B819E02F",
                ExecutionDurationMs = 1,
                RowsAffected = 5,
                RiskLevel = QueryRiskLevel.Safe,
                Success = true
            },
            new AuditLog
            {
                Id = Guid.NewGuid(),
                TenantId = Guid.Parse("00000000-0000-0000-0000-000000000001"),
                Timestamp = DateTime.UtcNow.AddMinutes(-25),
                ClientIp = "10.0.4.15",
                UserRole = "ADMIN",
                Action = "EXECUTE_QUERY",
                ConnectionName = "Azure SQL Server (MSSQL)",
                DatabaseType = DatabaseType.SqlServer,
                TargetDatabase = "enterprise_dw",
                QueryText = "SELECT TOP 10 [CustomerName], SUM([TotalAmount]) AS Spend FROM [Orders] GROUP BY [CustomerName];",
                QueryHash = "9F8102CD",
                ExecutionDurationMs = 3,
                RowsAffected = 5,
                RiskLevel = QueryRiskLevel.Safe,
                Success = true
            },
            new AuditLog
            {
                Id = Guid.NewGuid(),
                TenantId = Guid.Parse("00000000-0000-0000-0000-000000000001"),
                Timestamp = DateTime.UtcNow.AddMinutes(-45),
                ClientIp = "127.0.0.1",
                UserRole = "ADMIN",
                Action = "BACKUP_SNAPSHOT",
                ConnectionName = "StoreHub Enterprise Demo DB",
                DatabaseType = DatabaseType.Sqlite,
                TargetDatabase = "StoreHub_Demo_DB",
                QueryText = "DATABASE SNAPSHOT StoreHub_Daily_Snapshot_2026.bak",
                QueryHash = "SNAP991A",
                ExecutionDurationMs = 14,
                RowsAffected = 0,
                RiskLevel = QueryRiskLevel.Safe,
                Success = true
            }
        );

        // Seed Clean Pre-Configured BI Reports (Zero Execution Errors)
        dbContext.SavedReports.AddRange(
            new SavedReport
            {
                Id = Guid.NewGuid(),
                Title = "Revenue by Order Status",
                Description = "Aggregated customer order revenues grouped by fulfillment status.",
                QueryText = "SELECT Status AS Category, SUM(TotalAmount) AS Value FROM Orders GROUP BY Status ORDER BY Value DESC;",
                WidgetType = "ClusteredBarChart",
                CategoryColumn = "Category",
                MeasureColumn = "Value",
                CreatedAt = DateTime.UtcNow
            },
            new SavedReport
            {
                Id = Guid.NewGuid(),
                Title = "Average Salary by Department",
                Description = "Departmental compensation analysis across active personnel.",
                QueryText = "SELECT Department AS Category, AVG(Salary) AS Value FROM Employees GROUP BY Department ORDER BY Value DESC;",
                WidgetType = "ClusteredColumnChart",
                CategoryColumn = "Category",
                MeasureColumn = "Value",
                CreatedAt = DateTime.UtcNow
            },
            new SavedReport
            {
                Id = Guid.NewGuid(),
                Title = "Inventory Stock by Category",
                Description = "Current available warehouse unit counts by cloud hardware product category.",
                QueryText = "SELECT Category AS Category, SUM(StockQuantity) AS Value FROM Products GROUP BY Category ORDER BY Value DESC;",
                WidgetType = "DonutChart",
                CategoryColumn = "Category",
                MeasureColumn = "Value",
                CreatedAt = DateTime.UtcNow
            },
            new SavedReport
            {
                Id = Guid.NewGuid(),
                Title = "Top Customer Expenditures",
                Description = "High-value enterprise customer volume breakdown.",
                QueryText = "SELECT CustomerName AS Category, SUM(TotalAmount) AS Value FROM Orders GROUP BY CustomerName ORDER BY Value DESC LIMIT 10;",
                WidgetType = "PieChart",
                CategoryColumn = "Category",
                MeasureColumn = "Value",
                CreatedAt = DateTime.UtcNow
            },
            new SavedReport
            {
                Id = Guid.NewGuid(),
                Title = "Product Catalog Unit Pricing",
                Description = "Hardware catalog pricing distribution.",
                QueryText = "SELECT Name AS Category, AVG(Price) AS Value FROM Products GROUP BY Name ORDER BY Value DESC LIMIT 10;",
                WidgetType = "LineChart",
                CategoryColumn = "Category",
                MeasureColumn = "Value",
                CreatedAt = DateTime.UtcNow
            },
            new SavedReport
            {
                Id = Guid.NewGuid(),
                Title = "Department Headcount",
                Description = "Total headcount allocation per enterprise division.",
                QueryText = "SELECT Department AS Category, COUNT(Id) AS Value FROM Employees GROUP BY Department ORDER BY Value DESC;",
                WidgetType = "StackedBarChart",
                CategoryColumn = "Category",
                MeasureColumn = "Value",
                CreatedAt = DateTime.UtcNow
            }
        );

        dbContext.SaveChanges();
    }
}

// 10. Configure Middleware Pipeline
app.UseMiddleware<GlobalExceptionMiddleware>();
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseMiddleware<PerformanceProfilingMiddleware>();

app.UseDefaultFiles();
app.UseStaticFiles();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "DBMSOnline API v1");
    c.RoutePrefix = "swagger";
});

app.UseCors("AllowAngularSpa");
app.UseRateLimiter();
app.UseAuthorization();

app.MapControllers();
app.MapHub<DBMSOnline.Api.Hubs.QueryHub>("/hubs/query");
app.MapFallbackToFile("index.html");

app.Run();
