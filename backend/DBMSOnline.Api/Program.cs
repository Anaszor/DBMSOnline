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

// 4b. Register SignalR Hubs
builder.Services.AddSignalR();

// 5. Register FluentValidation
builder.Services.AddValidatorsFromAssemblyContaining<ConnectionRequestDtoValidator>();

// 6. Controllers & OpenAPI / Swagger
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.MaxDepth = 128;
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
    });

builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
{
    options.ValueLengthLimit = int.MaxValue;
    options.MultipartBodyLengthLimit = long.MaxValue;
    options.MemoryBufferThreshold = int.MaxValue;
});

builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.Limits.MaxRequestBodySize = null; // Unlimited request body size for large DB dumps
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

    // Create sample demo SQLite database file if not exists
    string demoDbPath = Path.Combine(AppContext.BaseDirectory, "demo_dbms.db");
    using (var conn = new SqliteConnection($"Data Source={demoDbPath}"))
    {
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS Employees (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                FullName TEXT NOT NULL,
                Position TEXT,
                Department TEXT,
                Salary REAL,
                Email TEXT,
                Password TEXT
            );

            CREATE TABLE IF NOT EXISTS Departments (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL,
                Location TEXT
            );

            INSERT OR IGNORE INTO Departments (Id, Name, Location) VALUES 
            (1, 'Engineering', 'Building A'),
            (2, 'Finance', 'Building B'),
            (3, 'Operations', 'Building C');

            INSERT OR IGNORE INTO Employees (Id, FullName, Position, Department, Salary, Email, Password) VALUES 
            (1, 'Alex Morgan', 'Senior Lead Engineer', 'Engineering', 125000, 'alex.morgan@company.com', 'Pass123!'),
            (2, 'Samantha Reed', 'Financial Analyst', 'Finance', 95000, 'samantha.r@company.com', 'SecretKey456'),
            (3, 'David Chen', 'DevOps Specialist', 'Engineering', 110000, 'david.c@company.com', 'EncryptedPass789');
        ";
        cmd.ExecuteNonQuery();
    }

    // Seed default Demo Connection & Cloud Connections if no connections exist
    if (!dbContext.SavedConnections.Any())
    {
        var enc = scope.ServiceProvider.GetRequiredService<IEncryptionService>();

        // 1. Render PostgreSQL Cloud DB
        dbContext.SavedConnections.Add(new SavedConnection
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.Parse("00000000-0000-0000-0000-000000000001"),
            CreatedByUserId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Name = "Render PostgreSQL Cloud DB",
            DatabaseType = DatabaseType.PostgreSql,
            AuthType = AuthType.ConnectionString,
            Host = "dpg-da2lh21t0dsc73amd580-a.oregon-postgres.render.com",
            Port = 5432,
            DatabaseName = "vva_postgres",
            Username = "vva_postgres_user",
            EncryptedConnectionString = enc.Encrypt("Host=dpg-da2lh21t0dsc73amd580-a.oregon-postgres.render.com;Port=5432;Database=vva_postgres;Username=vva_postgres_user;Password=HnPXLeEhkUQ3wwyK8iBKn1YwrNI2UVWS;SSL Mode=Require;Trust Server Certificate=true;"),
            IsReadOnly = false,
            EnforceSsl = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        // 2. Local SQL Server (MSSQL Express)
        dbContext.SavedConnections.Add(new SavedConnection
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.Parse("00000000-0000-0000-0000-000000000001"),
            CreatedByUserId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Name = "Local SQL Server (MSSQL)",
            DatabaseType = DatabaseType.SqlServer,
            AuthType = AuthType.WindowsAuth,
            Host = @"localhost\SQLEXPRESS",
            Port = 1433,
            DatabaseName = "master",
            Username = "sa",
            EncryptedPassword = "",
            IsReadOnly = false,
            EnforceSsl = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        // 3. Local PostgreSQL Engine
        dbContext.SavedConnections.Add(new SavedConnection
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.Parse("00000000-0000-0000-0000-000000000001"),
            CreatedByUserId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Name = "Local PostgreSQL DB",
            DatabaseType = DatabaseType.PostgreSql,
            AuthType = AuthType.BasicPassword,
            Host = "localhost",
            Port = 5432,
            DatabaseName = "postgres",
            Username = "postgres",
            EncryptedPassword = enc.Encrypt("postgres"),
            IsReadOnly = false,
            EnforceSsl = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        // 4. Local MySQL Engine
        dbContext.SavedConnections.Add(new SavedConnection
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.Parse("00000000-0000-0000-0000-000000000001"),
            CreatedByUserId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Name = "Local MySQL DB",
            DatabaseType = DatabaseType.MySql,
            AuthType = AuthType.BasicPassword,
            Host = "localhost",
            Port = 3306,
            DatabaseName = "mysql",
            Username = "root",
            EncryptedPassword = enc.Encrypt("root"),
            IsReadOnly = false,
            EnforceSsl = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        // 5. Local Demo SQLite DB
        dbContext.SavedConnections.Add(new SavedConnection
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.Parse("00000000-0000-0000-0000-000000000001"),
            CreatedByUserId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Name = "Local Demo SQLite DB",
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
