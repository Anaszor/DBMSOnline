using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Threading.Tasks;
using Npgsql;
using DBMSOnline.Application.DTOs;
using DBMSOnline.Application.Interfaces;
using DBMSOnline.Domain.Entities;
using DBMSOnline.Domain.Enums;

namespace DBMSOnline.Infrastructure.DbProviders;

public class PostgreSqlProvider : IDbProvider
{
    public DatabaseType DatabaseType => DatabaseType.PostgreSql;

    private static string ConvertPostgresUrlToConnectionString(string rawUrl, bool enforceSsl, string overrideDatabase = "")
    {
        if (string.IsNullOrWhiteSpace(rawUrl)) return string.Empty;

        rawUrl = rawUrl.Trim();
        if (!rawUrl.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase) && 
            !rawUrl.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase))
        {
            return rawUrl;
        }

        try
        {
            var match = System.Text.RegularExpressions.Regex.Match(
                rawUrl, 
                @"(?:postgresql|postgres)://(?:(?<user>[^:@/]+)(?::(?<pass>[^@/]*))?@)?(?<host>[^:/#\?]+)(?::(?<port>\d+))?/(?<db>[^\?#]+)?", 
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            if (match.Success)
            {
                string user = Uri.UnescapeDataString(match.Groups["user"].Value);
                string pass = Uri.UnescapeDataString(match.Groups["pass"].Value);
                string host = match.Groups["host"].Value;
                string portStr = match.Groups["port"].Value;
                string db = match.Groups["db"].Value;

                int port = !string.IsNullOrEmpty(portStr) ? int.Parse(portStr) : 5432;
                string targetDb = !string.IsNullOrWhiteSpace(overrideDatabase) 
                    ? overrideDatabase 
                    : (!string.IsNullOrWhiteSpace(db) ? db : "postgres");

                var builder = new NpgsqlConnectionStringBuilder
                {
                    Host = host,
                    Port = port,
                    Database = targetDb,
                    Username = user,
                    Password = pass,
                    SslMode = (enforceSsl || rawUrl.Contains("sslmode=require", StringComparison.OrdinalIgnoreCase)) ? SslMode.Require : SslMode.Prefer,
                    Timeout = 15
                };

                return builder.ToString();
            }
        }
        catch { }

        return rawUrl;
    }

    private string BuildConnectionString(SavedConnection connection, string databaseName, string decryptedPassword, string decryptedConnectionString)
    {
        if (!string.IsNullOrWhiteSpace(decryptedConnectionString))
        {
            return ConvertPostgresUrlToConnectionString(decryptedConnectionString, connection.EnforceSsl, databaseName);
        }

        var stdBuilder = new NpgsqlConnectionStringBuilder
        {
            Host = connection.Host,
            Port = connection.Port > 0 ? connection.Port : 5432,
            Database = string.IsNullOrWhiteSpace(databaseName) ? (string.IsNullOrWhiteSpace(connection.DatabaseName) ? "postgres" : connection.DatabaseName) : databaseName,
            Username = connection.Username,
            Password = decryptedPassword,
            SslMode = connection.EnforceSsl ? SslMode.Require : SslMode.Prefer,
            Timeout = 15
        };

        return stdBuilder.ToString();
    }

    public async Task<bool> TestConnectionAsync(SavedConnection connection, string decryptedPassword, string decryptedConnectionString)
    {
        string cs = BuildConnectionString(connection, connection.DatabaseName, decryptedPassword, decryptedConnectionString);
        using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync();
        return conn.State == ConnectionState.Open;
    }

    public async Task<List<string>> GetDatabasesAsync(SavedConnection connection, string decryptedPassword, string decryptedConnectionString)
    {
        string targetDb = !string.IsNullOrWhiteSpace(connection.DatabaseName) ? connection.DatabaseName : "postgres";
        string cs = BuildConnectionString(connection, targetDb, decryptedPassword, decryptedConnectionString);
        using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync();

        var databases = new List<string>();
        try
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT datname FROM pg_database WHERE datistemplate = false ORDER BY datname;";

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                databases.Add(reader.GetString(0));
            }
        }
        catch
        {
            if (!string.IsNullOrWhiteSpace(targetDb))
            {
                databases.Add(targetDb);
            }
        }
        return databases.Count > 0 ? databases : new List<string> { targetDb };
    }

    private static string FormatQualifiedTableName(string tableName)
    {
        if (string.IsNullOrWhiteSpace(tableName)) return "\"table\"";
        var parts = tableName.Replace("\"", "").Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2)
        {
            return $"\"{parts[0]}\".\"{parts[1]}\"";
        }
        return $"\"{parts[0]}\"";
    }

    public async Task<List<TableSchemaDto>> GetTablesAsync(SavedConnection connection, string databaseName, string decryptedPassword, string decryptedConnectionString)
    {
        string cs = BuildConnectionString(connection, databaseName, decryptedPassword, decryptedConnectionString);
        using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync();

        var tables = new List<TableSchemaDto>();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT table_schema, table_name, table_type FROM information_schema.tables WHERE table_schema NOT IN ('pg_catalog', 'information_schema') ORDER BY table_schema, table_name;";

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            string schema = reader.GetString(0);
            string name = reader.GetString(1);
            string displayName = schema.Equals("public", StringComparison.OrdinalIgnoreCase) ? name : $"{schema}.{name}";

            tables.Add(new TableSchemaDto
            {
                SchemaName = schema,
                TableName = displayName,
                TableType = reader.GetString(2).Contains("VIEW", StringComparison.OrdinalIgnoreCase) ? "VIEW" : "TABLE"
            });
        }
        return tables;
    }

    public async Task<List<ColumnSchemaDto>> GetColumnsAsync(SavedConnection connection, string databaseName, string tableName, string decryptedPassword, string decryptedConnectionString)
    {
        string cs = BuildConnectionString(connection, databaseName, decryptedPassword, decryptedConnectionString);
        using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync();

        var columns = new List<ColumnSchemaDto>();
        using var cmd = conn.CreateCommand();

        string rawTable = tableName.Replace("\"", "");
        string? schemaName = null;
        if (rawTable.Contains('.'))
        {
            var parts = rawTable.Split('.', 2);
            schemaName = parts[0];
            rawTable = parts[1];
        }

        string querySql = @"
            SELECT 
                c.column_name, 
                c.data_type, 
                c.is_nullable, 
                c.column_default,
                CASE WHEN pk.column_name IS NOT NULL THEN true ELSE false END AS is_primary_key,
                CASE WHEN fk.column_name IS NOT NULL THEN true ELSE false END AS is_foreign_key
            FROM information_schema.columns c
            LEFT JOIN (
                SELECT kcu.column_name, kcu.table_name, kcu.table_schema
                FROM information_schema.table_constraints tc
                JOIN information_schema.key_column_usage kcu 
                  ON tc.constraint_name = kcu.constraint_name AND tc.table_schema = kcu.table_schema
                WHERE tc.constraint_type = 'PRIMARY KEY'
            ) pk ON c.table_name = pk.table_name AND c.column_name = pk.column_name AND c.table_schema = pk.table_schema
            LEFT JOIN (
                SELECT kcu.column_name, kcu.table_name, kcu.table_schema
                FROM information_schema.table_constraints tc
                JOIN information_schema.key_column_usage kcu 
                  ON tc.constraint_name = kcu.constraint_name AND tc.table_schema = kcu.table_schema
                WHERE tc.constraint_type = 'FOREIGN KEY'
            ) fk ON c.table_name = fk.table_name AND c.column_name = fk.column_name AND c.table_schema = fk.table_schema
            WHERE c.table_name = @tableName ";

        if (!string.IsNullOrEmpty(schemaName))
        {
            cmd.CommandText = querySql + " AND c.table_schema = @schemaName ORDER BY c.ordinal_position;";
            cmd.Parameters.AddWithValue("@tableName", rawTable);
            cmd.Parameters.AddWithValue("@schemaName", schemaName);
        }
        else
        {
            cmd.CommandText = querySql + " ORDER BY c.ordinal_position;";
            cmd.Parameters.AddWithValue("@tableName", rawTable);
        }

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            columns.Add(new ColumnSchemaDto
            {
                ColumnName = reader.GetString(0),
                DataType = reader.GetString(1),
                IsNullable = reader.GetString(2).Equals("YES", StringComparison.OrdinalIgnoreCase),
                DefaultValue = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                IsPrimaryKey = reader.GetBoolean(4),
                IsForeignKey = reader.GetBoolean(5)
            });
        }
        return columns;
    }

    public async Task<QueryResultDto> ExecuteQueryAsync(SavedConnection connection, string databaseName, string queryText, int maxRows, int timeoutSeconds, string decryptedPassword, string decryptedConnectionString)
    {
        var sw = Stopwatch.StartNew();
        string cs = BuildConnectionString(connection, databaseName, decryptedPassword, decryptedConnectionString);
        using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync();

        if (connection.IsReadOnly)
        {
            using var roCmd = conn.CreateCommand();
            roCmd.CommandText = "SET TRANSACTION READ ONLY;";
            await roCmd.ExecuteNonQueryAsync();
        }

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
        string cs = BuildConnectionString(connection, request.DatabaseName, decryptedPassword, decryptedConnectionString);
        using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync();

        var columns = await GetColumnsAsync(connection, request.DatabaseName, request.TableName, decryptedPassword, decryptedConnectionString);
        string qualifiedTable = FormatQualifiedTableName(request.TableName);

        using var countCmd = conn.CreateCommand();
        countCmd.CommandTimeout = 300;
        countCmd.CommandText = $"SELECT COUNT(*) FROM {qualifiedTable};";
        long totalRows = Convert.ToInt64(await countCmd.ExecuteScalarAsync());

        int offset = (request.Page - 1) * request.PageSize;
        string orderClause = !string.IsNullOrEmpty(request.SortColumn)
            ? $"ORDER BY \"{request.SortColumn}\" {(request.SortAscending ? "ASC" : "DESC")}"
            : "";

        using var pageCmd = conn.CreateCommand();
        pageCmd.CommandTimeout = 300;
        pageCmd.CommandText = $"SELECT * FROM {qualifiedTable} {orderClause} LIMIT {request.PageSize} OFFSET {offset};";

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
        string cs = BuildConnectionString(connection, request.DatabaseName, decryptedPassword, decryptedConnectionString);
        using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync();

        var cols = new List<string>();
        var vals = new List<string>();
        using var cmd = conn.CreateCommand();
        string qualifiedTable = FormatQualifiedTableName(request.TableName);

        int pIdx = 0;
        foreach (var kvp in request.Data)
        {
            cols.Add($"\"{kvp.Key}\"");
            string pName = $"@p{pIdx}";
            vals.Add(pName);
            cmd.Parameters.AddWithValue(pName, kvp.Value ?? DBNull.Value);
            pIdx++;
        }

        cmd.CommandText = $"INSERT INTO {qualifiedTable} ({string.Join(", ", cols)}) VALUES ({string.Join(", ", vals)});";
        return await cmd.ExecuteNonQueryAsync();
    }

    public async Task<int> UpdateRowAsync(SavedConnection connection, RowMutationRequestDto request, string decryptedPassword, string decryptedConnectionString)
    {
        string cs = BuildConnectionString(connection, request.DatabaseName, decryptedPassword, decryptedConnectionString);
        using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        var setClauses = new List<string>();
        var whereClauses = new List<string>();
        string qualifiedTable = FormatQualifiedTableName(request.TableName);

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

        cmd.CommandText = $"UPDATE {qualifiedTable} SET {string.Join(", ", setClauses)} WHERE {string.Join(" AND ", whereClauses)};";
        return await cmd.ExecuteNonQueryAsync();
    }

    public async Task<int> DeleteRowAsync(SavedConnection connection, RowMutationRequestDto request, string decryptedPassword, string decryptedConnectionString)
    {
        string cs = BuildConnectionString(connection, request.DatabaseName, decryptedPassword, decryptedConnectionString);
        using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        var whereClauses = new List<string>();
        string qualifiedTable = FormatQualifiedTableName(request.TableName);

        int pIdx = 0;
        foreach (var kvp in request.PrimaryKey)
        {
            string pName = $"@pk{pIdx}";
            whereClauses.Add($"\"{kvp.Key}\" = {pName}");
            cmd.Parameters.AddWithValue(pName, kvp.Value ?? DBNull.Value);
            pIdx++;
        }

        cmd.CommandText = $"DELETE FROM {qualifiedTable} WHERE {string.Join(" AND ", whereClauses)};";
        return await cmd.ExecuteNonQueryAsync();
    }

    public async Task<List<StoredProcedureDto>> GetStoredProceduresAsync(SavedConnection connection, string databaseName, string decryptedPassword, string decryptedConnectionString)
    {
        string cs = BuildConnectionString(connection, databaseName, decryptedPassword, decryptedConnectionString);
        using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync();

        var list = new List<StoredProcedureDto>();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT routine_schema, routine_name, routine_type, routine_definition
            FROM information_schema.routines
            WHERE routine_schema NOT IN ('pg_catalog', 'information_schema')
            ORDER BY routine_type, routine_name;";

        try
        {
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new StoredProcedureDto
                {
                    SchemaName = reader.GetString(0),
                    ProcedureName = reader.GetString(1),
                    RoutineType = reader.GetString(2),
                    Definition = reader.IsDBNull(3) ? string.Empty : reader.GetString(3)
                });
            }
        }
        catch { }
        return list;
    }

    public async Task<List<TableRelationDto>> GetRelationsAsync(SavedConnection connection, string databaseName, string decryptedPassword, string decryptedConnectionString)
    {
        string cs = BuildConnectionString(connection, databaseName, decryptedPassword, decryptedConnectionString);
        using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync();

        var list = new List<TableRelationDto>();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT
                tc.table_name AS PKTable,
                kcu.column_name AS PKColumn,
                ccu.table_name AS FKTable,
                ccu.column_name AS FKColumn,
                tc.constraint_name AS ConstraintName
            FROM information_schema.table_constraints tc
            JOIN information_schema.key_column_usage kcu ON tc.constraint_name = kcu.constraint_name
            JOIN information_schema.constraint_column_usage ccu ON ccu.constraint_name = tc.constraint_name
            WHERE tc.constraint_type = 'FOREIGN KEY';";

        try
        {
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new TableRelationDto
                {
                    PKTable = reader.GetString(0),
                    PKColumn = reader.GetString(1),
                    FKTable = reader.GetString(2),
                    FKColumn = reader.GetString(3),
                    ConstraintName = reader.GetString(4)
                });
            }
        }
        catch { }
        return list;
    }
}
