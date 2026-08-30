using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using DBMSOnline.Application.DTOs;
using DBMSOnline.Application.Interfaces;
using DBMSOnline.Domain.Entities;
using DBMSOnline.Domain.Enums;

namespace DBMSOnline.Infrastructure.DbProviders;

public class SqliteProvider : IDbProvider
{
    public DatabaseType DatabaseType => DatabaseType.Sqlite;

    private string BuildConnectionString(SavedConnection connection, string decryptedConnectionString)
    {
        if (!string.IsNullOrWhiteSpace(decryptedConnectionString))
            return decryptedConnectionString;

        string dataSource = string.IsNullOrWhiteSpace(connection.DatabaseName) ? "demo_dbms.db" : connection.DatabaseName;
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = dataSource,
            Mode = connection.IsReadOnly ? SqliteOpenMode.ReadOnly : SqliteOpenMode.ReadWriteCreate
        };
        return builder.ToString();
    }

    public async Task<bool> TestConnectionAsync(SavedConnection connection, string decryptedPassword, string decryptedConnectionString)
    {
        string cs = BuildConnectionString(connection, decryptedConnectionString);
        using var conn = new SqliteConnection(cs);
        await conn.OpenAsync();
        return conn.State == ConnectionState.Open;
    }

    public async Task<List<string>> GetDatabasesAsync(SavedConnection connection, string decryptedPassword, string decryptedConnectionString)
    {
        // SQLite uses single database file per connection
        string dbName = string.IsNullOrWhiteSpace(connection.DatabaseName) ? "main.db" : connection.DatabaseName;
        return await Task.FromResult(new List<string> { dbName });
    }

    public async Task<List<TableSchemaDto>> GetTablesAsync(SavedConnection connection, string databaseName, string decryptedPassword, string decryptedConnectionString)
    {
        string cs = BuildConnectionString(connection, decryptedConnectionString);
        using var conn = new SqliteConnection(cs);
        await conn.OpenAsync();

        var tables = new List<TableSchemaDto>();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT name, type FROM sqlite_master WHERE type IN ('table', 'view') AND name NOT LIKE 'sqlite_%' ORDER BY name;";

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            tables.Add(new TableSchemaDto
            {
                TableName = reader.GetString(0),
                SchemaName = "main",
                TableType = reader.GetString(1).ToUpperInvariant(),
                ApproxRowCount = 0
            });
        }
        return tables;
    }

    public async Task<List<ColumnSchemaDto>> GetColumnsAsync(SavedConnection connection, string databaseName, string tableName, string decryptedPassword, string decryptedConnectionString)
    {
        string cs = BuildConnectionString(connection, decryptedConnectionString);
        using var conn = new SqliteConnection(cs);
        await conn.OpenAsync();

        var columns = new List<ColumnSchemaDto>();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"PRAGMA table_info('{tableName}');";

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            columns.Add(new ColumnSchemaDto
            {
                ColumnName = reader.GetString(1),
                DataType = reader.GetString(2),
                IsNullable = reader.GetInt32(3) == 0,
                IsPrimaryKey = reader.GetInt32(5) > 0,
                DefaultValue = reader.IsDBNull(4) ? string.Empty : reader.GetString(4)
            });
        }
        return columns;
    }

    public async Task<QueryResultDto> ExecuteQueryAsync(SavedConnection connection, string databaseName, string queryText, int maxRows, int timeoutSeconds, string decryptedPassword, string decryptedConnectionString)
    {
        var sw = Stopwatch.StartNew();
        string cs = BuildConnectionString(connection, decryptedConnectionString);
        using var conn = new SqliteConnection(cs);
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = queryText;
        cmd.CommandTimeout = timeoutSeconds;

        var result = new QueryResultDto();

        try
        {
            using var reader = await cmd.ExecuteReaderAsync();
            if (reader.HasRows)
            {
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    result.Columns.Add(reader.GetName(i));
                }

                int count = 0;
                while (await reader.ReadAsync())
                {
                    if (count >= maxRows)
                    {
                        result.IsTruncated = true;
                        break;
                    }

                    var row = new Dictionary<string, object?>();
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                    }
                    result.Rows.Add(row);
                    count++;
                }
                result.RowsAffected = count;
            }
            else
            {
                result.RowsAffected = reader.RecordsAffected;
            }
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
        }

        sw.Stop();
        result.ExecutionDurationMs = sw.ElapsedMilliseconds;
        return result;
    }

    public async Task<DataPageResultDto> GetTableDataAsync(SavedConnection connection, DataPageRequestDto request, string decryptedPassword, string decryptedConnectionString)
    {
        string cs = BuildConnectionString(connection, decryptedConnectionString);
        using var conn = new SqliteConnection(cs);
        await conn.OpenAsync();

        var columns = await GetColumnsAsync(connection, request.DatabaseName, request.TableName, decryptedPassword, decryptedConnectionString);

        // Count total rows
        using var countCmd = conn.CreateCommand();
        countCmd.CommandText = $"SELECT COUNT(*) FROM \"{request.TableName}\";";
        long totalRows = Convert.ToInt64(await countCmd.ExecuteScalarAsync());

        // Fetch Page
        int offset = (request.Page - 1) * request.PageSize;
        string orderClause = !string.IsNullOrEmpty(request.SortColumn) 
            ? $"ORDER BY \"{request.SortColumn}\" {(request.SortAscending ? "ASC" : "DESC")}" 
            : "";

        using var pageCmd = conn.CreateCommand();
        pageCmd.CommandText = $"SELECT * FROM \"{request.TableName}\" {orderClause} LIMIT {request.PageSize} OFFSET {offset};";

        var rows = new List<Dictionary<string, object?>>();
        using var reader = await pageCmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var row = new Dictionary<string, object?>();
            for (int i = 0; i < reader.FieldCount; i++)
            {
                row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
            }
            rows.Add(row);
        }

        return new DataPageResultDto
        {
            Columns = columns,
            Rows = rows,
            TotalRows = totalRows,
            Page = request.Page,
            PageSize = request.PageSize
        };
    }

    public async Task<int> InsertRowAsync(SavedConnection connection, RowMutationRequestDto request, string decryptedPassword, string decryptedConnectionString)
    {
        string cs = BuildConnectionString(connection, decryptedConnectionString);
        using var conn = new SqliteConnection(cs);
        await conn.OpenAsync();

        var cols = new List<string>();
        var vals = new List<string>();
        using var cmd = conn.CreateCommand();

        int pIdx = 0;
        foreach (var kvp in request.Data)
        {
            cols.Add($"\"{kvp.Key}\"");
            string pName = $"@p{pIdx}";
            vals.Add(pName);
            cmd.Parameters.AddWithValue(pName, kvp.Value ?? DBNull.Value);
            pIdx++;
        }

        cmd.CommandText = $"INSERT INTO \"{request.TableName}\" ({string.Join(", ", cols)}) VALUES ({string.Join(", ", vals)});";
        return await cmd.ExecuteNonQueryAsync();
    }

    public async Task<int> UpdateRowAsync(SavedConnection connection, RowMutationRequestDto request, string decryptedPassword, string decryptedConnectionString)
    {
        string cs = BuildConnectionString(connection, decryptedConnectionString);
        using var conn = new SqliteConnection(cs);
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        var setClauses = new List<string>();
        var whereClauses = new List<string>();

        int pIdx = 0;
        foreach (var kvp in request.Data)
        {
            if (request.PrimaryKey.ContainsKey(kvp.Key))
                continue;

            string pName = $"@p{pIdx}";
            setClauses.Add($"\"{kvp.Key}\" = {pName}");
            cmd.Parameters.AddWithValue(pName, kvp.Value ?? DBNull.Value);
            pIdx++;
        }

        if (setClauses.Count == 0)
            return 0;

        foreach (var kvp in request.PrimaryKey)
        {
            string pName = $"@pk{pIdx}";
            whereClauses.Add($"\"{kvp.Key}\" = {pName}");
            cmd.Parameters.AddWithValue(pName, kvp.Value ?? DBNull.Value);
            pIdx++;
        }

        cmd.CommandText = $"UPDATE \"{request.TableName}\" SET {string.Join(", ", setClauses)} WHERE {string.Join(" AND ", whereClauses)};";
        return await cmd.ExecuteNonQueryAsync();
    }

    public async Task<int> DeleteRowAsync(SavedConnection connection, RowMutationRequestDto request, string decryptedPassword, string decryptedConnectionString)
    {
        string cs = BuildConnectionString(connection, decryptedConnectionString);
        using var conn = new SqliteConnection(cs);
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        var whereClauses = new List<string>();

        int pIdx = 0;
        foreach (var kvp in request.PrimaryKey)
        {
            string pName = $"@pk{pIdx}";
            whereClauses.Add($"\"{kvp.Key}\" = {pName}");
            cmd.Parameters.AddWithValue(pName, kvp.Value ?? DBNull.Value);
            pIdx++;
        }

        cmd.CommandText = $"DELETE FROM \"{request.TableName}\" WHERE {string.Join(" AND ", whereClauses)};";
        return await cmd.ExecuteNonQueryAsync();
    }

    public async Task<List<StoredProcedureDto>> GetStoredProceduresAsync(SavedConnection connection, string databaseName, string decryptedPassword, string decryptedConnectionString)
    {
        return await Task.FromResult(new List<StoredProcedureDto>());
    }

    public async Task<List<TableRelationDto>> GetRelationsAsync(SavedConnection connection, string databaseName, string decryptedPassword, string decryptedConnectionString)
    {
        return await Task.FromResult(new List<TableRelationDto>());
    }
}
