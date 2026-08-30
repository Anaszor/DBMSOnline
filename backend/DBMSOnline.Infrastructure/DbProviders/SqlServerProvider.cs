using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using DBMSOnline.Application.DTOs;
using DBMSOnline.Application.Interfaces;
using DBMSOnline.Domain.Entities;
using DBMSOnline.Domain.Enums;

namespace DBMSOnline.Infrastructure.DbProviders;

public class SqlServerProvider : IDbProvider
{
    public DatabaseType DatabaseType => DatabaseType.SqlServer;

    private string BuildConnectionString(SavedConnection connection, string databaseName, string decryptedPassword, string decryptedConnectionString)
    {
        string serverHost = connection.Host ?? "";
        if (serverHost.Contains('/') && serverHost.Contains('\\'))
        {
            // Format 10.0.30.143/DESKTOP-E23R0C2\SQLEXPRESS -> 10.0.30.143\SQLEXPRESS
            var parts = serverHost.Split('\\');
            var ipPart = parts[0].Split('/')[0];
            serverHost = $"{ipPart}\\{parts[1]}";
        }
        else if (serverHost.Contains('/'))
        {
            serverHost = serverHost.Split('/')[0];
        }

        var builder = new SqlConnectionStringBuilder
        {
            DataSource = $"{serverHost}{(connection.Port > 0 && connection.Port != 1433 ? $",{connection.Port}" : "")}",
            InitialCatalog = string.IsNullOrWhiteSpace(databaseName) ? (string.IsNullOrWhiteSpace(connection.DatabaseName) ? "master" : connection.DatabaseName) : databaseName,
            IntegratedSecurity = connection.AuthType == AuthType.WindowsAuth,
            TrustServerCertificate = true,
            Encrypt = connection.EnforceSsl,
            ApplicationIntent = connection.IsReadOnly ? ApplicationIntent.ReadOnly : ApplicationIntent.ReadWrite,
            ConnectTimeout = 15
        };

        if (connection.AuthType == AuthType.BasicPassword)
        {
            builder.UserID = connection.Username;
            builder.Password = decryptedPassword;
        }

        return builder.ToString();
    }

    public async Task<bool> TestConnectionAsync(SavedConnection connection, string decryptedPassword, string decryptedConnectionString)
    {
        string cs = BuildConnectionString(connection, connection.DatabaseName, decryptedPassword, decryptedConnectionString);
        using var conn = new SqlConnection(cs);
        await conn.OpenAsync();
        return conn.State == ConnectionState.Open;
    }

    public async Task<List<string>> GetDatabasesAsync(SavedConnection connection, string decryptedPassword, string decryptedConnectionString)
    {
        string cs = BuildConnectionString(connection, "master", decryptedPassword, decryptedConnectionString);
        using var conn = new SqlConnection(cs);
        await conn.OpenAsync();

        var databases = new List<string>();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT name FROM sys.databases WHERE state = 0 AND name NOT IN ('master', 'tempdb', 'model', 'msdb') ORDER BY name;";

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            databases.Add(reader.GetString(0));
        }
        return databases;
    }

    public async Task<List<TableSchemaDto>> GetTablesAsync(SavedConnection connection, string databaseName, string decryptedPassword, string decryptedConnectionString)
    {
        string cs = BuildConnectionString(connection, databaseName, decryptedPassword, decryptedConnectionString);
        using var conn = new SqlConnection(cs);
        await conn.OpenAsync();

        var tables = new List<TableSchemaDto>();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT TABLE_SCHEMA, TABLE_NAME, TABLE_TYPE FROM INFORMATION_SCHEMA.TABLES ORDER BY TABLE_SCHEMA, TABLE_NAME;";

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            tables.Add(new TableSchemaDto
            {
                SchemaName = reader.GetString(0),
                TableName = reader.GetString(1),
                TableType = reader.GetString(2).Contains("VIEW", StringComparison.OrdinalIgnoreCase) ? "VIEW" : "TABLE"
            });
        }
        return tables;
    }

    public async Task<List<ColumnSchemaDto>> GetColumnsAsync(SavedConnection connection, string databaseName, string tableName, string decryptedPassword, string decryptedConnectionString)
    {
        string cs = BuildConnectionString(connection, databaseName, decryptedPassword, decryptedConnectionString);
        using var conn = new SqlConnection(cs);
        await conn.OpenAsync();

        var columns = new List<ColumnSchemaDto>();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT 
                c.COLUMN_NAME, 
                c.DATA_TYPE, 
                c.IS_NULLABLE, 
                c.COLUMN_DEFAULT, 
                c.CHARACTER_MAXIMUM_LENGTH,
                CASE WHEN pk.COLUMN_NAME IS NOT NULL THEN 1 ELSE 0 END AS IS_PRIMARY_KEY,
                CASE WHEN fk.COLUMN_NAME IS NOT NULL THEN 1 ELSE 0 END AS IS_FOREIGN_KEY,
                COLUMNPROPERTY(OBJECT_ID(c.TABLE_SCHEMA + '.' + c.TABLE_NAME), c.COLUMN_NAME, 'IsIdentity') AS IS_IDENTITY
            FROM INFORMATION_SCHEMA.COLUMNS c
            LEFT JOIN (
                SELECT kcu.COLUMN_NAME, kcu.TABLE_NAME, kcu.TABLE_SCHEMA
                FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
                JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE kcu 
                  ON tc.CONSTRAINT_NAME = kcu.CONSTRAINT_NAME AND tc.TABLE_SCHEMA = kcu.TABLE_SCHEMA
                WHERE tc.CONSTRAINT_TYPE = 'PRIMARY KEY'
            ) pk ON c.TABLE_NAME = pk.TABLE_NAME AND c.COLUMN_NAME = pk.COLUMN_NAME AND c.TABLE_SCHEMA = pk.TABLE_SCHEMA
            LEFT JOIN (
                SELECT kcu.COLUMN_NAME, kcu.TABLE_NAME, kcu.TABLE_SCHEMA
                FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
                JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE kcu 
                  ON tc.CONSTRAINT_NAME = kcu.CONSTRAINT_NAME AND tc.TABLE_SCHEMA = kcu.TABLE_SCHEMA
                WHERE tc.CONSTRAINT_TYPE = 'FOREIGN KEY'
            ) fk ON c.TABLE_NAME = fk.TABLE_NAME AND c.COLUMN_NAME = fk.COLUMN_NAME AND c.TABLE_SCHEMA = fk.TABLE_SCHEMA
            WHERE c.TABLE_NAME = @tableName
            ORDER BY c.ORDINAL_POSITION;";
        cmd.Parameters.AddWithValue("@tableName", tableName.Replace("[", "").Replace("]", ""));

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            columns.Add(new ColumnSchemaDto
            {
                ColumnName = reader.GetString(0),
                DataType = reader.GetString(1),
                IsNullable = reader.GetString(2).Equals("YES", StringComparison.OrdinalIgnoreCase),
                DefaultValue = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                MaxLength = reader.IsDBNull(4) ? 0 : reader.GetInt32(4),
                IsPrimaryKey = reader.GetInt32(5) == 1,
                IsForeignKey = reader.GetInt32(6) == 1,
                IsAutoIncrement = !reader.IsDBNull(7) && reader.GetInt32(7) == 1
            });
        }
        return columns;
    }

    public async Task<QueryResultDto> ExecuteQueryAsync(SavedConnection connection, string databaseName, string queryText, int maxRows, int timeoutSeconds, string decryptedPassword, string decryptedConnectionString)
    {
        var sw = Stopwatch.StartNew();
        string cs = BuildConnectionString(connection, databaseName, decryptedPassword, decryptedConnectionString);
        using var conn = new SqlConnection(cs);
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = queryText;
        cmd.CommandTimeout = timeoutSeconds;

        var result = new QueryResultDto();
        try
        {
            using var reader = await cmd.ExecuteReaderAsync();
            do
            {
                if (reader.FieldCount > 0)
                {
                    result.Columns.Clear();
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

                    if (result.Rows.Count > 0 || result.Columns.Count > 0)
                    {
                        break;
                    }
                }
            } while (await reader.NextResultAsync());

            if (result.Columns.Count == 0 && result.Rows.Count == 0)
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
        using var conn = new SqlConnection(cs);
        await conn.OpenAsync();

        var columns = await GetColumnsAsync(connection, request.DatabaseName, request.TableName, decryptedPassword, decryptedConnectionString);

        using var countCmd = conn.CreateCommand();
        countCmd.CommandTimeout = 300;
        countCmd.CommandText = $"SELECT COUNT(*) FROM [{request.TableName}];";
        long totalRows = Convert.ToInt64(await countCmd.ExecuteScalarAsync());

        int offset = (request.Page - 1) * request.PageSize;
        string orderClause = !string.IsNullOrEmpty(request.SortColumn)
            ? $"ORDER BY [{request.SortColumn}] {(request.SortAscending ? "ASC" : "DESC")}"
            : "ORDER BY (SELECT NULL)";

        using var pageCmd = conn.CreateCommand();
        pageCmd.CommandTimeout = 300;
        pageCmd.CommandText = $"SELECT * FROM [{request.TableName}] {orderClause} OFFSET {offset} ROWS FETCH NEXT {request.PageSize} ROWS ONLY;";

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
        using var conn = new SqlConnection(cs);
        await conn.OpenAsync();

        var cols = new List<string>();
        var vals = new List<string>();
        using var cmd = conn.CreateCommand();

        int pIdx = 0;
        foreach (var kvp in request.Data)
        {
            cols.Add($"[{kvp.Key}]");
            string pName = $"@p{pIdx}";
            vals.Add(pName);
            cmd.Parameters.AddWithValue(pName, kvp.Value ?? DBNull.Value);
            pIdx++;
        }

        cmd.CommandText = $"INSERT INTO [{request.TableName}] ({string.Join(", ", cols)}) VALUES ({string.Join(", ", vals)});";
        return await cmd.ExecuteNonQueryAsync();
    }

    public async Task<int> UpdateRowAsync(SavedConnection connection, RowMutationRequestDto request, string decryptedPassword, string decryptedConnectionString)
    {
        string cs = BuildConnectionString(connection, request.DatabaseName, decryptedPassword, decryptedConnectionString);
        using var conn = new SqlConnection(cs);
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
            setClauses.Add($"[{kvp.Key}] = {pName}");
            cmd.Parameters.AddWithValue(pName, kvp.Value ?? DBNull.Value);
            pIdx++;
        }

        if (setClauses.Count == 0)
            return 0;

        foreach (var kvp in request.PrimaryKey)
        {
            string pName = $"@pk{pIdx}";
            whereClauses.Add($"[{kvp.Key}] = {pName}");
            cmd.Parameters.AddWithValue(pName, kvp.Value ?? DBNull.Value);
            pIdx++;
        }

        cmd.CommandText = $"UPDATE [{request.TableName}] SET {string.Join(", ", setClauses)} WHERE {string.Join(" AND ", whereClauses)};";
        return await cmd.ExecuteNonQueryAsync();
    }

    public async Task<int> DeleteRowAsync(SavedConnection connection, RowMutationRequestDto request, string decryptedPassword, string decryptedConnectionString)
    {
        string cs = BuildConnectionString(connection, request.DatabaseName, decryptedPassword, decryptedConnectionString);
        using var conn = new SqlConnection(cs);
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        var whereClauses = new List<string>();

        int pIdx = 0;
        foreach (var kvp in request.PrimaryKey)
        {
            string pName = $"@pk{pIdx}";
            whereClauses.Add($"[{kvp.Key}] = {pName}");
            cmd.Parameters.AddWithValue(pName, kvp.Value ?? DBNull.Value);
            pIdx++;
        }

        cmd.CommandText = $"DELETE FROM [{request.TableName}] WHERE {string.Join(" AND ", whereClauses)};";
        return await cmd.ExecuteNonQueryAsync();
    }

    public async Task<List<StoredProcedureDto>> GetStoredProceduresAsync(SavedConnection connection, string databaseName, string decryptedPassword, string decryptedConnectionString)
    {
        string cs = BuildConnectionString(connection, databaseName, decryptedPassword, decryptedConnectionString);
        using var conn = new SqlConnection(cs);
        await conn.OpenAsync();

        var list = new List<StoredProcedureDto>();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT ROUTINE_SCHEMA, ROUTINE_NAME, ROUTINE_TYPE, ROUTINE_DEFINITION
            FROM INFORMATION_SCHEMA.ROUTINES
            ORDER BY ROUTINE_TYPE, ROUTINE_NAME;";

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
            reader.Close();

            // Fetch parameter details for each stored procedure
            foreach (var sp in list)
            {
                using var pCmd = conn.CreateCommand();
                pCmd.CommandText = @"
                    SELECT PARAMETER_NAME, DATA_TYPE, PARAMETER_MODE
                    FROM INFORMATION_SCHEMA.PARAMETERS
                    WHERE SPECIFIC_SCHEMA = @schema AND SPECIFIC_NAME = @proc
                    ORDER BY ORDINAL_POSITION;";
                pCmd.Parameters.AddWithValue("@schema", sp.SchemaName);
                pCmd.Parameters.AddWithValue("@proc", sp.ProcedureName);

                using var pReader = await pCmd.ExecuteReaderAsync();
                while (await pReader.ReadAsync())
                {
                    string pName = pReader.IsDBNull(0) ? string.Empty : pReader.GetString(0);
                    if (!string.IsNullOrWhiteSpace(pName))
                    {
                        sp.Parameters.Add(new ProcedureParameterDto
                        {
                            ParameterName = pName,
                            DataType = pReader.IsDBNull(1) ? string.Empty : pReader.GetString(1),
                            ParameterMode = pReader.IsDBNull(2) ? "IN" : pReader.GetString(2)
                        });
                    }
                }
            }
        }
        catch { }
        return list;
    }

    public async Task<List<TableRelationDto>> GetRelationsAsync(SavedConnection connection, string databaseName, string decryptedPassword, string decryptedConnectionString)
    {
        string cs = BuildConnectionString(connection, databaseName, decryptedPassword, decryptedConnectionString);
        using var conn = new SqlConnection(cs);
        await conn.OpenAsync();

        var list = new List<TableRelationDto>();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT 
                pk_t.NAME AS PKTable, 
                pk_c.NAME AS PKColumn, 
                fk_t.NAME AS FKTable, 
                fk_c.NAME AS FKColumn, 
                fk.NAME AS ConstraintName
            FROM sys.foreign_keys fk
            INNER JOIN sys.foreign_key_columns fkc ON fk.object_id = fkc.constraint_object_id
            INNER JOIN sys.tables fk_t ON fkc.parent_object_id = fk_t.object_id
            INNER JOIN sys.columns fk_c ON fkc.parent_object_id = fk_c.object_id AND fkc.parent_column_id = fk_c.column_id
            INNER JOIN sys.tables pk_t ON fkc.referenced_object_id = pk_t.object_id
            INNER JOIN sys.columns pk_c ON fkc.referenced_object_id = pk_c.object_id AND fkc.referenced_column_id = pk_c.column_id;";

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
