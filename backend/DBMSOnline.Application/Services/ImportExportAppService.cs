using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using DBMSOnline.Application.DTOs;
using DBMSOnline.Application.Interfaces;
using DBMSOnline.Domain.Enums;
using DBMSOnline.Domain.Exceptions;

namespace DBMSOnline.Application.Services;

public class ImportExportAppService : IImportExportAppService
{
    private readonly IConnectionAppService _connectionAppService;
    private readonly IDbProviderFactory _providerFactory;
    private readonly ISchemaAppService _schemaAppService;

    public ImportExportAppService(
        IConnectionAppService connectionAppService,
        IDbProviderFactory providerFactory,
        ISchemaAppService schemaAppService)
    {
        _connectionAppService = connectionAppService;
        _providerFactory = providerFactory;
        _schemaAppService = schemaAppService;
    }

    public async Task<ExportResultDto> ExportTableAsync(ExportTableRequestDto request)
    {
        var (connection, password, cs) = await _connectionAppService.ResolveConnectionCredentialsAsync(request.ConnectionId);
        var provider = _providerFactory.GetProvider(connection.DatabaseType);

        string format = (request.Format ?? "sql").ToLowerInvariant();
        string dbName = string.IsNullOrEmpty(request.DatabaseName) ? connection.DatabaseName : request.DatabaseName;

        // Fetch Columns
        var columns = await provider.GetColumnsAsync(connection, dbName, request.TableName, password, cs);
        
        // Fetch Table Data
        var dataPage = await provider.GetTableDataAsync(connection, new DataPageRequestDto
        {
            ConnectionId = request.ConnectionId,
            DatabaseName = dbName,
            TableName = request.TableName,
            Page = 1,
            PageSize = 50000 // Export up to 50k rows
        }, password, cs);

        var result = new ExportResultDto();
        var sb = new StringBuilder();

        if (format == "csv")
        {
            result.FileName = $"{request.TableName}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv";
            result.ContentType = "text/csv";

            // Headers
            var colNames = columns.Select(c => QuoteCsv(c.ColumnName)).ToList();
            sb.AppendLine(string.Join(",", colNames));

            // Rows
            foreach (var row in dataPage.Rows)
            {
                var values = columns.Select(c =>
                {
                    row.TryGetValue(c.ColumnName, out var val);
                    return QuoteCsv(val?.ToString());
                });
                sb.AppendLine(string.Join(",", values));
            }
        }
        else if (format == "json")
        {
            result.FileName = $"{request.TableName}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.json";
            result.ContentType = "application/json";
            sb.Append(JsonSerializer.Serialize(dataPage.Rows, new JsonSerializerOptions { WriteIndented = true }));
        }
        else // Default SQL format
        {
            result.FileName = $"{request.TableName}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.sql";
            result.ContentType = "application/sql";

            sb.AppendLine($"-- DBMSOnline Studio Export");
            sb.AppendLine($"-- Database: {dbName} | Table: {request.TableName}");
            sb.AppendLine($"-- Engine: {connection.DatabaseType}");
            sb.AppendLine($"-- Generated At: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
            sb.AppendLine();

            if (request.IncludeSchema)
            {
                sb.AppendLine($"-- Schema Definition");
                sb.AppendLine($"CREATE TABLE {QuoteIdentifier(request.TableName, connection.DatabaseType)} (");
                var colDefs = columns.Select(c =>
                {
                    string nullability = c.IsNullable ? "NULL" : "NOT NULL";
                    string pk = c.IsPrimaryKey ? " PRIMARY KEY" : "";
                    return $"    {QuoteIdentifier(c.ColumnName, connection.DatabaseType)} {c.DataType} {nullability}{pk}";
                });
                sb.AppendLine(string.Join(",\n", colDefs));
                sb.AppendLine(");");
                sb.AppendLine();
            }

            if (request.IncludeData && dataPage.Rows.Count > 0)
            {
                sb.AppendLine($"-- Data Records ({dataPage.Rows.Count} rows)");
                foreach (var row in dataPage.Rows)
                {
                    var colList = string.Join(", ", columns.Select(c => QuoteIdentifier(c.ColumnName, connection.DatabaseType)));
                    var valList = string.Join(", ", columns.Select(c =>
                    {
                        row.TryGetValue(c.ColumnName, out var val);
                        return FormatSqlValue(val, c.DataType);
                    }));
                    sb.AppendLine($"INSERT INTO {QuoteIdentifier(request.TableName, connection.DatabaseType)} ({colList}) VALUES ({valList});");
                }
            }
        }

        result.Content = sb.ToString();
        result.FileSizeBytes = Encoding.UTF8.GetByteCount(result.Content);
        result.Success = true;
        result.Message = $"Successfully exported table '{request.TableName}' to {format.ToUpper()} format.";
        return result;
    }

    public async Task<ExportResultDto> ExportDatabaseAsync(ExportDatabaseRequestDto request)
    {
        var (connection, password, cs) = await _connectionAppService.ResolveConnectionCredentialsAsync(request.ConnectionId);
        var provider = _providerFactory.GetProvider(connection.DatabaseType);
        string dbName = string.IsNullOrEmpty(request.DatabaseName) ? connection.DatabaseName : request.DatabaseName;

        var tables = await provider.GetTablesAsync(connection, dbName, password, cs);
        var sb = new StringBuilder();

        sb.AppendLine($"-- ========================================================");
        sb.AppendLine($"-- DBMSOnline Studio Full Database Export");
        sb.AppendLine($"-- Database: {dbName}");
        sb.AppendLine($"-- Engine: {connection.DatabaseType}");
        sb.AppendLine($"-- Date: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine($"-- ========================================================");
        sb.AppendLine();

        foreach (var tbl in tables)
        {
            var tableExport = await ExportTableAsync(new ExportTableRequestDto
            {
                ConnectionId = request.ConnectionId,
                DatabaseName = dbName,
                TableName = tbl.TableName,
                Format = "sql",
                IncludeSchema = true,
                IncludeData = true
            });

            sb.AppendLine(tableExport.Content);
            sb.AppendLine();
        }

        string resultString = sb.ToString();
        return new ExportResultDto
        {
            Success = true,
            FileName = $"{dbName}_FullBackup_{DateTime.UtcNow:yyyyMMdd_HHmmss}.sql",
            ContentType = "application/sql",
            Content = resultString,
            FileSizeBytes = Encoding.UTF8.GetByteCount(resultString),
            Message = $"Full database export generated for '{dbName}' ({tables.Count} tables)."
        };
    }

    public async Task<ImportResultDto> ImportTableDataAsync(ImportTableRequestDto request)
    {
        var (connection, password, cs) = await _connectionAppService.ResolveConnectionCredentialsAsync(request.ConnectionId);
        var provider = _providerFactory.GetProvider(connection.DatabaseType);
        string dbName = string.IsNullOrEmpty(request.DatabaseName) ? connection.DatabaseName : request.DatabaseName;

        if (string.IsNullOrWhiteSpace(request.FileContent))
            throw new DomainException("Import content is empty.");

        int rowsInserted = 0;
        var errors = new List<string>();

        if (request.FileFormat.ToLowerInvariant() == "csv")
        {
            using var reader = new StringReader(request.FileContent);
            string? headerLine = await reader.ReadLineAsync();
            if (headerLine == null) throw new DomainException("CSV content missing header row.");

            var headers = parseCsvLine(headerLine);

            string? line;
            while ((line = await reader.ReadLineAsync()) != null)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                var values = parseCsvLine(line);

                var rowData = new Dictionary<string, object?>();
                for (int i = 0; i < Math.Min(headers.Count, values.Count); i++)
                {
                    rowData[headers[i]] = values[i];
                }

                try
                {
                    await provider.InsertRowAsync(connection, new RowMutationRequestDto
                    {
                        ConnectionId = request.ConnectionId,
                        DatabaseName = dbName,
                        TableName = request.TableName,
                        Data = rowData
                    }, password, cs);
                    rowsInserted++;
                }
                catch (Exception ex)
                {
                    errors.Add($"Row {rowsInserted + 1}: {ex.Message}");
                }
            }
        }
        else if (request.FileFormat.ToLowerInvariant() == "json")
        {
            var rows = JsonSerializer.Deserialize<List<Dictionary<string, object?>>>(request.FileContent);
            if (rows != null)
            {
                foreach (var rowData in rows)
                {
                    try
                    {
                        await provider.InsertRowAsync(connection, new RowMutationRequestDto
                        {
                            ConnectionId = request.ConnectionId,
                            DatabaseName = dbName,
                            TableName = request.TableName,
                            Data = rowData
                        }, password, cs);
                        rowsInserted++;
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"Row {rowsInserted + 1}: {ex.Message}");
                    }
                }
            }
        }

        return new ImportResultDto
        {
            Success = errors.Count == 0,
            RowsImported = rowsInserted,
            StatementsExecuted = rowsInserted,
            Message = $"Successfully imported {rowsInserted} rows into '{request.TableName}'.",
            Errors = errors
        };
    }

    public async Task<ImportResultDto> ImportDatabaseScriptAsync(ImportDatabaseRequestDto request)
    {
        var (connection, password, cs) = await _connectionAppService.ResolveConnectionCredentialsAsync(request.ConnectionId);
        var provider = _providerFactory.GetProvider(connection.DatabaseType);
        string dbName = string.IsNullOrEmpty(request.DatabaseName) ? connection.DatabaseName : request.DatabaseName;

        if (string.IsNullOrWhiteSpace(request.SqlScript))
            throw new DomainException("SQL script content is empty.");

        var statements = request.SqlScript.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        int executed = 0;
        var errors = new List<string>();

        foreach (var stmt in statements)
        {
            if (string.IsNullOrWhiteSpace(stmt)) continue;
            try
            {
                await provider.ExecuteQueryAsync(connection, dbName, stmt, 1000, 30, password, cs);
                executed++;
            }
            catch (Exception ex)
            {
                errors.Add($"Statement {executed + 1}: {ex.Message}");
            }
        }

        return new ImportResultDto
        {
            Success = errors.Count == 0,
            RowsImported = 0,
            StatementsExecuted = executed,
            Message = $"Executed {executed} SQL statements against '{dbName}'.",
            Errors = errors
        };
    }

    private static string QuoteIdentifier(string id, DatabaseType dbType)
    {
        if (dbType == DatabaseType.SqlServer) return $"[{id}]";
        if (dbType == DatabaseType.MySql) return $"`{id}`";
        return $"\"{id}\"";
    }

    private static string QuoteCsv(string? val)
    {
        if (val == null) return "";
        if (val.Contains(',') || val.Contains('"') || val.Contains('\n'))
        {
            return $"\"{val.Replace("\"", "\"\"")}\"";
        }
        return val;
    }

    private static string FormatSqlValue(object? val, string dataType)
    {
        if (val == null || val is DBNull) return "NULL";
        string s = val.ToString() ?? "";

        if (dataType.Contains("INT", StringComparison.OrdinalIgnoreCase) ||
            dataType.Contains("REAL", StringComparison.OrdinalIgnoreCase) ||
            dataType.Contains("FLOAT", StringComparison.OrdinalIgnoreCase) ||
            dataType.Contains("DECIMAL", StringComparison.OrdinalIgnoreCase) ||
            dataType.Contains("NUMERIC", StringComparison.OrdinalIgnoreCase))
        {
            return double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out double num) 
                ? num.ToString(CultureInfo.InvariantCulture) 
                : "0";
        }

        if (dataType.Contains("BOOL", StringComparison.OrdinalIgnoreCase))
        {
            return (s.Equals("true", StringComparison.OrdinalIgnoreCase) || s == "1") ? "1" : "0";
        }

        return $"'{s.Replace("'", "''")}'";
    }

    private static List<string> parseCsvLine(string line)
    {
        var result = new List<string>();
        bool inQuotes = false;
        var current = new StringBuilder();

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (c == ',' && !inQuotes)
            {
                result.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }
        result.Add(current.ToString());
        return result;
    }
}
