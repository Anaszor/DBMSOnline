using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using DBMSOnline.Domain.Enums;
using DBMSOnline.Domain.Exceptions;
using DBMSOnline.Domain.Interfaces;

namespace DBMSOnline.Infrastructure.Security;

public class QuerySafetyEngine : IQuerySafetyService
{
    private static readonly Regex StackedQueryRegex = new(@"\b(SELECT|INSERT|UPDATE|DELETE|CREATE|ALTER|DROP|TRUNCATE)\b.*;\s*\b(SELECT|INSERT|UPDATE|DELETE|CREATE|ALTER|DROP|TRUNCATE)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    
    private static readonly Regex DangerousProceduresRegex = new(@"\b(xp_cmdshell|sp_oacreate|sp_oamethod|sys_exec|sys_eval|LOAD_FILE|INTO\s+OUTFILE|INTO\s+DUMPFILE|COPY\s+.*\s+TO\s+PROGRAM)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex WriteDdlRegex = new(@"\b(INSERT|UPDATE|DELETE|CREATE|ALTER|DROP|TRUNCATE|GRANT|REVOKE|MERGE)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public (QueryRiskLevel riskLevel, string warningMessage, bool isAllowed) AnalyzeQuery(string query, DatabaseType dbType, bool isReadOnlyConnection)
    {
        if (string.IsNullOrWhiteSpace(query))
            return (QueryRiskLevel.Safe, string.Empty, true);

        string trimmedQuery = query.Trim();

        // 1. Check for Dangerous System Procedures / OS Execution
        if (DangerousProceduresRegex.IsMatch(trimmedQuery))
        {
            return (QueryRiskLevel.Critical, "Query contains dangerous system execution procedures or OS file operations.", false);
        }

        // 2. Check Read-Only Connection Enforcement
        if (isReadOnlyConnection && WriteDdlRegex.IsMatch(trimmedQuery))
        {
            return (QueryRiskLevel.High, "Write/DDL/DML operations are strictly blocked on Read-Only connections.", false);
        }

        // 3. Check Multi-Statement Stacked Queries
        if (StackedQueryRegex.IsMatch(trimmedQuery))
        {
            return (QueryRiskLevel.High, "Stacked multi-statement queries detected. Executing multiple statements in a single batch requires caution.", true);
        }

        // 4. High Risk DDL Detection (DROP, TRUNCATE, ALTER)
        if (Regex.IsMatch(trimmedQuery, @"\b(DROP\s+DATABASE|DROP\s+TABLE|TRUNCATE\s+TABLE)\b", RegexOptions.IgnoreCase))
        {
            return (QueryRiskLevel.High, "Destructive DDL query detected (DROP / TRUNCATE).", true);
        }

        // 5. Moderate Risk DML Detection (DELETE WITHOUT WHERE)
        if (Regex.IsMatch(trimmedQuery, @"\bDELETE\s+FROM\b", RegexOptions.IgnoreCase) && !Regex.IsMatch(trimmedQuery, @"\bWHERE\b", RegexOptions.IgnoreCase))
        {
            return (QueryRiskLevel.Moderate, "DELETE statement without WHERE clause detected.", true);
        }

        return (QueryRiskLevel.Safe, string.Empty, true);
    }

    public string SanitizeConnectionString(string connectionString, DatabaseType dbType)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            return string.Empty;

        // Strip dangerous options
        string sanitized = connectionString;

        // MySQL security: disable Allow Local Infile
        if (dbType == DatabaseType.MySql)
        {
            sanitized = Regex.Replace(sanitized, @"Allow\s*Local\s*Infile\s*=\s*true", "Allow Local Infile=false", RegexOptions.IgnoreCase);
            if (!sanitized.Contains("Allow Local Infile=", StringComparison.OrdinalIgnoreCase))
            {
                sanitized += ";Allow Local Infile=false";
            }
        }

        // MS SQL Server security: enforce TrustServerCertificate=True only if development
        if (dbType == DatabaseType.SqlServer)
        {
            if (!sanitized.Contains("Encrypt=", StringComparison.OrdinalIgnoreCase))
            {
                sanitized += ";Encrypt=Optional";
            }
        }

        return sanitized;
    }
}
