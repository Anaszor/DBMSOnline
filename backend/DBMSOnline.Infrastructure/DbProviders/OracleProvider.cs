using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Oracle.ManagedDataAccess.Client;
using DBMSOnline.Application.DTOs;
using DBMSOnline.Application.Interfaces;
using DBMSOnline.Domain.Entities;
using DBMSOnline.Domain.Enums;

namespace DBMSOnline.Infrastructure.DbProviders;

public class OracleProvider : IDbProvider
{
    public DatabaseType DatabaseType => DatabaseType.Oracle;

    private string BuildConnectionString(SavedConnection connection, string databaseName, string decryptedPassword, string decryptedConnectionString)
    {
        if (!string.IsNullOrWhiteSpace(decryptedConnectionString))
            return decryptedConnectionString;

        string host = string.IsNullOrWhiteSpace(connection.Host) ? "localhost" : connection.Host;
        int port = connection.Port <= 0 ? 1521 : connection.Port;
        string db = string.IsNullOrWhiteSpace(databaseName) ? (string.IsNullOrWhiteSpace(connection.DatabaseName) ? "XE" : connection.DatabaseName) : databaseName;

        return $"Data Source=(DESCRIPTION=(ADDRESS=(PROTOCOL=TCP)(HOST={host})(PORT={port}))(CONNECT_DATA=(SERVICE_NAME={db})));User Id={connection.Username};Password={decryptedPassword};";
    }

    public async Task<bool> TestConnectionAsync(SavedConnection connection, string decryptedPassword, string decryptedConnectionString)
    {
        string cs = BuildConnectionString(connection, connection.DatabaseName, decryptedPassword, decryptedConnectionString);
        using var conn = new OracleConnection(cs);
        await conn.OpenAsync();
        return conn.State == ConnectionState.Open;
    }

    public async Task<List<string>> GetDatabasesAsync(SavedConnection connection, string decryptedPassword, string decryptedConnectionString)
    {
        string cs = BuildConnectionString(connection, connection.DatabaseName, decryptedPassword, decryptedConnectionString);
        using var conn = new OracleConnection(cs);
        await conn.OpenAsync();

        var list = new List<string>();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT USERNAME FROM ALL_USERS ORDER BY USERNAME";

        try
        {
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(reader.GetString(0));
            }
        }
        catch
        {
            list.Add(string.IsNullOrWhiteSpace(connection.DatabaseName) ? "XE" : connection.DatabaseName);
        }
        return list;
    }

    public async Task<List<TableSchemaDto>> GetTablesAsync(SavedConnection connection, string databaseName, string decryptedPassword, string decryptedConnectionString)
    {
        string cs = BuildConnectionString(connection, databaseName, decryptedPassword, decryptedConnectionString);
        using var conn = new OracleConnection(cs);
        await conn.OpenAsync();

        var list = new List<TableSchemaDto>();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT TABLE_NAME, 'TABLE' AS TABLE_TYPE FROM USER_TABLES
            UNION ALL
            SELECT VIEW_NAME AS TABLE_NAME, 'VIEW' AS TABLE_TYPE FROM USER_VIEWS
            ORDER BY TABLE_NAME";

        try
        {
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new TableSchemaDto
                {
                    TableName = reader.GetString(0),
                    SchemaName = connection.Username.ToUpperInvariant(),
                    TableType = reader.GetString(1),
                    ApproxRowCount = 0
                });
            }
        }
        catch { }
        return list;
    }

    public async Task<List<ColumnSchemaDto>> GetColumnsAsync(SavedConnection connection, string databaseName, string tableName, string decryptedPassword, string decryptedConnectionString)
    {
        string cs = BuildConnectionString(connection, databaseName, decryptedPassword, decryptedConnectionString);
        using var conn = new OracleConnection(cs);
        await conn.OpenAsync();

        var list = new List<ColumnSchemaDto>();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT 
                c.COLUMN_NAME, 
                c.DATA_TYPE, 
                c.NULLABLE, 
                c.DATA_LENGTH,
                CASE WHEN pk.COLUMN_NAME IS NOT NULL THEN 1 ELSE 0 END AS IS_PK,
                CASE WHEN fk.COLUMN_NAME IS NOT NULL THEN 1 ELSE 0 END AS IS_FK
            FROM USER_TAB_COLUMNS c
            LEFT JOIN (
                SELECT acc.COLUMN_NAME 
                FROM USER_CONSTRAINTS ac 
                JOIN USER_CONS_COLUMNS acc ON ac.CONSTRAINT_NAME = acc.CONSTRAINT_NAME 
                WHERE ac.CONSTRAINT_TYPE = 'P' AND UPPER(ac.TABLE_NAME) = UPPER(:tbl)
            ) pk ON c.COLUMN_NAME = pk.COLUMN_NAME
            LEFT JOIN (
                SELECT acc.COLUMN_NAME 
                FROM USER_CONSTRAINTS ac 
                JOIN USER_CONS_COLUMNS acc ON ac.CONSTRAINT_NAME = acc.CONSTRAINT_NAME 
                WHERE ac.CONSTRAINT_TYPE = 'R' AND UPPER(ac.TABLE_NAME) = UPPER(:tbl)
            ) fk ON c.COLUMN_NAME = fk.COLUMN_NAME
            WHERE UPPER(c.TABLE_NAME) = UPPER(:tbl)
            ORDER BY c.COLUMN_ID";

        cmd.Parameters.Add(new OracleParameter("tbl", tableName));

        try
        {
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new ColumnSchemaDto
                {
                    ColumnName = reader.GetString(0),
                    DataType = reader.GetString(1),
                    IsNullable = reader.GetString(2) == "Y",
                    MaxLength = reader.IsDBNull(3) ? 0 : Convert.ToInt32(reader.GetValue(3)),
                    IsPrimaryKey = Convert.ToInt32(reader.GetValue(4)) == 1,
                    IsForeignKey = Convert.ToInt32(reader.GetValue(5)) == 1
                });
            }
        }
        catch { }
        return list;
    }

    public async Task<List<StoredProcedureDto>> GetStoredProceduresAsync(SavedConnection connection, string databaseName, string decryptedPassword, string decryptedConnectionString)
    {
        string cs = BuildConnectionString(connection, databaseName, decryptedPassword, decryptedConnectionString);
        using var conn = new OracleConnection(cs);
        await conn.OpenAsync();

        var list = new List<StoredProcedureDto>();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT OBJECT_NAME, OBJECT_TYPE 
            FROM USER_OBJECTS 
            WHERE OBJECT_TYPE IN ('PROCEDURE', 'FUNCTION') 
            ORDER BY OBJECT_NAME";

        try
        {
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new StoredProcedureDto
                {
                    SchemaName = connection.Username.ToUpperInvariant(),
                    ProcedureName = reader.GetString(0),
                    RoutineType = reader.GetString(1),
                    Definition = string.Empty
                });
            }
            reader.Close();

            foreach (var sp in list)
            {
                using var pCmd = conn.CreateCommand();
                pCmd.CommandText = @"
                    SELECT ARGUMENT_NAME, DATA_TYPE, IN_OUT 
                    FROM USER_ARGUMENTS 
                    WHERE OBJECT_NAME = :objName AND ARGUMENT_NAME IS NOT NULL 
                    ORDER BY POSITION";
                pCmd.Parameters.Add(new OracleParameter("objName", sp.ProcedureName));

                using var pReader = await pCmd.ExecuteReaderAsync();
                while (await pReader.ReadAsync())
                {
                    sp.Parameters.Add(new ProcedureParameterDto
                    {
                        ParameterName = pReader.GetString(0),
                        DataType = pReader.GetString(1),
                        ParameterMode = pReader.GetString(2)
                    });
                }
            }
        }
        catch { }
        return list;
    }

    public async Task<List<TableRelationDto>> GetRelationsAsync(SavedConnection connection, string databaseName, string decryptedPassword, string decryptedConnectionString)
    {
        string cs = BuildConnectionString(connection, databaseName, decryptedPassword, decryptedConnectionString);
        using var conn = new OracleConnection(cs);
        await conn.OpenAsync();

        var list = new List<TableRelationDto>();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT 
                r_acc.TABLE_NAME AS PK_TABLE,
                r_acc.COLUMN_NAME AS PK_COLUMN,
                acc.TABLE_NAME AS FK_TABLE,
                acc.COLUMN_NAME AS FK_COLUMN,
                ac.CONSTRAINT_NAME
            FROM USER_CONSTRAINTS ac
            JOIN USER_CONS_COLUMNS acc ON ac.CONSTRAINT_NAME = acc.CONSTRAINT_NAME
            JOIN USER_CONSTRAINTS r_ac ON ac.R_CONSTRAINT_NAME = r_ac.CONSTRAINT_NAME
            JOIN USER_CONS_COLUMNS r_acc ON r_ac.CONSTRAINT_NAME = r_acc.CONSTRAINT_NAME AND acc.POSITION = r_acc.POSITION
            WHERE ac.CONSTRAINT_TYPE = 'R'";

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

    public async Task<QueryResultDto> ExecuteQueryAsync(SavedConnection connection, string databaseName, string queryText, int maxRows, int timeoutSeconds, string decryptedPassword, string decryptedConnectionString)
    {
        var sw = Stopwatch.StartNew();
        var result = new QueryResultDto { Success = true };

        string cs = BuildConnectionString(connection, databaseName, decryptedPassword, decryptedConnectionString);
        using var conn = new OracleConnection(cs);
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = queryText;

        try
        {
            using var reader = await cmd.ExecuteReaderAsync();
            if (reader.HasRows)
            {
                var schemaTable = reader.GetSchemaTable();
                if (schemaTable != null)
                {
                    foreach (DataRow row in schemaTable.Rows)
                    {
                        result.Columns.Add(row["ColumnName"].ToString() ?? string.Empty);
                    }
                }

                int count = 0;
                while (await reader.ReadAsync() && count < maxRows)
                {
                    var rowDict = new Dictionary<string, object?>();
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        string colName = reader.GetName(i);
                        object val = reader.GetValue(i);
                        rowDict[colName] = val == DBNull.Value ? null : val.ToString();
                    }
                    result.Rows.Add(rowDict);
                    count++;
                }
                result.RowsAffected = count;
                result.IsTruncated = count >= maxRows;
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

    private static string QuoteOracleTable(string tableName)
    {
        if (string.IsNullOrWhiteSpace(tableName)) return tableName;
        string clean = tableName.Replace("\"", "").Trim();
        if (clean.Contains('.'))
        {
            var parts = clean.Split('.', 2);
            return $"\"{parts[0]}\".\"{parts[1]}\"";
        }
        return $"\"{clean}\"";
    }

    public async Task<DataPageResultDto> GetTableDataAsync(SavedConnection connection, DataPageRequestDto request, string decryptedPassword, string decryptedConnectionString)
    {
        string cs = BuildConnectionString(connection, request.DatabaseName, decryptedPassword, decryptedConnectionString);
        using var conn = new OracleConnection(cs);
        await conn.OpenAsync();

        var cols = await GetColumnsAsync(connection, request.DatabaseName, request.TableName, decryptedPassword, decryptedConnectionString);
        string quotedTable = QuoteOracleTable(request.TableName);

        using var countCmd = conn.CreateCommand();
        countCmd.CommandText = $"SELECT COUNT(*) FROM {quotedTable}";
        long totalRows = Convert.ToInt64(await countCmd.ExecuteScalarAsync());

        int offset = (request.Page - 1) * request.PageSize;
        using var dataCmd = conn.CreateCommand();
        dataCmd.CommandText = $"SELECT * FROM {quotedTable} OFFSET {offset} ROWS FETCH NEXT {request.PageSize} ROWS ONLY";

        var pageResult = new DataPageResultDto
        {
            Columns = cols,
            Page = request.Page,
            PageSize = request.PageSize,
            TotalRows = totalRows
        };

        using var reader = await dataCmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var rowDict = new Dictionary<string, object?>();
            for (int i = 0; i < reader.FieldCount; i++)
            {
                rowDict[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
            }
            pageResult.Rows.Add(rowDict);
        }

        return pageResult;
    }

    public async Task<int> InsertRowAsync(SavedConnection connection, RowMutationRequestDto request, string decryptedPassword, string decryptedConnectionString)
    {
        string cs = BuildConnectionString(connection, request.DatabaseName, decryptedPassword, decryptedConnectionString);
        using var conn = new OracleConnection(cs);
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        var cols = new List<string>();
        var vals = new List<string>();

        int pIdx = 0;
        foreach (var kvp in request.Data)
        {
            string pName = $":p{pIdx}";
            cols.Add($"\"{kvp.Key}\"");
            vals.Add(pName);
            cmd.Parameters.Add(new OracleParameter(pName, kvp.Value ?? DBNull.Value));
            pIdx++;
        }

        cmd.CommandText = $"INSERT INTO \"{request.TableName}\" ({string.Join(", ", cols)}) VALUES ({string.Join(", ", vals)})";
        return await cmd.ExecuteNonQueryAsync();
    }

    public async Task<int> UpdateRowAsync(SavedConnection connection, RowMutationRequestDto request, string decryptedPassword, string decryptedConnectionString)
    {
        string cs = BuildConnectionString(connection, request.DatabaseName, decryptedPassword, decryptedConnectionString);
        using var conn = new OracleConnection(cs);
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        var setClauses = new List<string>();
        var whereClauses = new List<string>();

        int pIdx = 0;
        foreach (var kvp in request.Data)
        {
            if (request.PrimaryKey.ContainsKey(kvp.Key))
                continue;

            string pName = $":p{pIdx}";
            setClauses.Add($"\"{kvp.Key}\" = {pName}");
            cmd.Parameters.Add(new OracleParameter(pName, kvp.Value ?? DBNull.Value));
            pIdx++;
        }

        if (setClauses.Count == 0) return 0;

        foreach (var kvp in request.PrimaryKey)
        {
            string pName = $":pk{pIdx}";
            whereClauses.Add($"\"{kvp.Key}\" = {pName}");
            cmd.Parameters.Add(new OracleParameter(pName, kvp.Value ?? DBNull.Value));
            pIdx++;
        }

        cmd.CommandText = $"UPDATE \"{request.TableName}\" SET {string.Join(", ", setClauses)} WHERE {string.Join(" AND ", whereClauses)}";
        return await cmd.ExecuteNonQueryAsync();
    }

    public async Task<int> DeleteRowAsync(SavedConnection connection, RowMutationRequestDto request, string decryptedPassword, string decryptedConnectionString)
    {
        string cs = BuildConnectionString(connection, request.DatabaseName, decryptedPassword, decryptedConnectionString);
        using var conn = new OracleConnection(cs);
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        var whereClauses = new List<string>();

        int pIdx = 0;
        foreach (var kvp in request.PrimaryKey)
        {
            string pName = $":pk{pIdx}";
            whereClauses.Add($"\"{kvp.Key}\" = {pName}");
            cmd.Parameters.Add(new OracleParameter(pName, kvp.Value ?? DBNull.Value));
            pIdx++;
        }

        cmd.CommandText = $"DELETE FROM \"{request.TableName}\" WHERE {string.Join(" AND ", whereClauses)}";
        return await cmd.ExecuteNonQueryAsync();
    }
}
