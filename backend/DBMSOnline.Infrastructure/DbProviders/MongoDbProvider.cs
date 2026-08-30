using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Driver;
using DBMSOnline.Application.DTOs;
using DBMSOnline.Application.Interfaces;
using DBMSOnline.Domain.Entities;
using DBMSOnline.Domain.Enums;

namespace DBMSOnline.Infrastructure.DbProviders;

public class MongoDbProvider : IDbProvider
{
    public DatabaseType DatabaseType => DatabaseType.MongoDb;

    private MongoClient GetClient(SavedConnection connection, string decryptedPassword, string decryptedConnectionString)
    {
        if (!string.IsNullOrWhiteSpace(decryptedConnectionString))
            return new MongoClient(decryptedConnectionString);

        string portStr = connection.Port > 0 ? $":{connection.Port}" : ":27017";
        string authPart = !string.IsNullOrWhiteSpace(connection.Username) ? $"{connection.Username}:{decryptedPassword}@" : "";
        string uri = $"mongodb://{authPart}{connection.Host}{portStr}";
        return new MongoClient(uri);
    }

    public async Task<bool> TestConnectionAsync(SavedConnection connection, string decryptedPassword, string decryptedConnectionString)
    {
        var client = GetClient(connection, decryptedPassword, decryptedConnectionString);
        var dbs = await client.ListDatabaseNamesAsync();
        return await dbs.AnyAsync();
    }

    public async Task<List<string>> GetDatabasesAsync(SavedConnection connection, string decryptedPassword, string decryptedConnectionString)
    {
        var client = GetClient(connection, decryptedPassword, decryptedConnectionString);
        var cursor = await client.ListDatabaseNamesAsync();
        return await cursor.ToListAsync();
    }

    public async Task<List<TableSchemaDto>> GetTablesAsync(SavedConnection connection, string databaseName, string decryptedPassword, string decryptedConnectionString)
    {
        var client = GetClient(connection, decryptedPassword, decryptedConnectionString);
        var db = client.GetDatabase(string.IsNullOrWhiteSpace(databaseName) ? "admin" : databaseName);
        var cursor = await db.ListCollectionNamesAsync();
        var collections = await cursor.ToListAsync();

        return collections.Select(c => new TableSchemaDto
        {
            TableName = c,
            SchemaName = "public",
            TableType = "COLLECTION"
        }).ToList();
    }

    public async Task<List<ColumnSchemaDto>> GetColumnsAsync(SavedConnection connection, string databaseName, string tableName, string decryptedPassword, string decryptedConnectionString)
    {
        var client = GetClient(connection, decryptedPassword, decryptedConnectionString);
        var db = client.GetDatabase(databaseName);
        var col = db.GetCollection<BsonDocument>(tableName);
        var sampleDoc = await col.Find(new BsonDocument()).FirstOrDefaultAsync();

        var columns = new List<ColumnSchemaDto>();
        if (sampleDoc != null)
        {
            foreach (var elem in sampleDoc.Elements)
            {
                columns.Add(new ColumnSchemaDto
                {
                    ColumnName = elem.Name,
                    DataType = elem.Value.BsonType.ToString(),
                    IsPrimaryKey = elem.Name == "_id",
                    IsNullable = true
                });
            }
        }
        else
        {
            columns.Add(new ColumnSchemaDto { ColumnName = "_id", DataType = "ObjectId", IsPrimaryKey = true });
        }
        return columns;
    }

    public async Task<QueryResultDto> ExecuteQueryAsync(SavedConnection connection, string databaseName, string queryText, int maxRows, int timeoutSeconds, string decryptedPassword, string decryptedConnectionString)
    {
        var sw = Stopwatch.StartNew();
        var client = GetClient(connection, decryptedPassword, decryptedConnectionString);
        var db = client.GetDatabase(databaseName);
        var result = new QueryResultDto();

        try
        {
            // Simple MongoDB JSON filter query syntax support: e.g. collectionName: { filter json }
            string targetCollection = "system";
            string filterJson = "{}";

            if (queryText.Contains(":"))
            {
                var parts = queryText.Split(new[] { ':' }, 2);
                targetCollection = parts[0].Trim();
                filterJson = parts[1].Trim();
            }

            var col = db.GetCollection<BsonDocument>(targetCollection);
            BsonDocument filterDoc = string.IsNullOrWhiteSpace(filterJson) || filterJson == "{}"
                ? new BsonDocument()
                : BsonDocument.Parse(filterJson);

            var docs = await col.Find(filterDoc).Limit(maxRows).ToListAsync();

            if (docs.Any())
            {
                var cols = docs.SelectMany(d => d.Names).Distinct().ToList();
                result.Columns = cols;

                foreach (var doc in docs)
                {
                    var row = new Dictionary<string, object?>();
                    foreach (var c in cols)
                    {
                        row[c] = doc.Contains(c) ? doc[c].ToString() : null;
                    }
                    result.Rows.Add(row);
                }
                result.RowsAffected = docs.Count;
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
        var client = GetClient(connection, decryptedPassword, decryptedConnectionString);
        var db = client.GetDatabase(request.DatabaseName);
        var col = db.GetCollection<BsonDocument>(request.TableName);

        long totalRows = await col.CountDocumentsAsync(new BsonDocument());
        var columns = await GetColumnsAsync(connection, request.DatabaseName, request.TableName, decryptedPassword, decryptedConnectionString);

        int skip = (request.Page - 1) * request.PageSize;
        var docs = await col.Find(new BsonDocument()).Skip(skip).Limit(request.PageSize).ToListAsync();

        var rows = new List<Dictionary<string, object?>>();
        foreach (var doc in docs)
        {
            var row = new Dictionary<string, object?>();
            foreach (var colDef in columns)
            {
                row[colDef.ColumnName] = doc.Contains(colDef.ColumnName) ? doc[colDef.ColumnName].ToString() : null;
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
        var client = GetClient(connection, decryptedPassword, decryptedConnectionString);
        var db = client.GetDatabase(request.DatabaseName);
        var col = db.GetCollection<BsonDocument>(request.TableName);

        var doc = new BsonDocument();
        foreach (var kvp in request.Data)
        {
            doc[kvp.Key] = kvp.Value != null ? BsonValue.Create(kvp.Value) : BsonNull.Value;
        }

        await col.InsertOneAsync(doc);
        return 1;
    }

    public async Task<int> UpdateRowAsync(SavedConnection connection, RowMutationRequestDto request, string decryptedPassword, string decryptedConnectionString)
    {
        var client = GetClient(connection, decryptedPassword, decryptedConnectionString);
        var db = client.GetDatabase(request.DatabaseName);
        var col = db.GetCollection<BsonDocument>(request.TableName);

        var filter = new BsonDocument();
        foreach (var pk in request.PrimaryKey)
        {
            if (pk.Key == "_id" && ObjectId.TryParse(pk.Value?.ToString(), out var oid))
                filter["_id"] = oid;
            else
                filter[pk.Key] = pk.Value?.ToString();
        }

        var update = new BsonDocument();
        foreach (var kvp in request.Data)
        {
            update[kvp.Key] = kvp.Value != null ? BsonValue.Create(kvp.Value) : BsonNull.Value;
        }

        var res = await col.UpdateOneAsync(filter, new BsonDocument("$set", update));
        return (int)res.ModifiedCount;
    }

    public async Task<int> DeleteRowAsync(SavedConnection connection, RowMutationRequestDto request, string decryptedPassword, string decryptedConnectionString)
    {
        var client = GetClient(connection, decryptedPassword, decryptedConnectionString);
        var db = client.GetDatabase(request.DatabaseName);
        var col = db.GetCollection<BsonDocument>(request.TableName);

        var filter = new BsonDocument();
        foreach (var pk in request.PrimaryKey)
        {
            if (pk.Key == "_id" && ObjectId.TryParse(pk.Value?.ToString(), out var oid))
                filter["_id"] = oid;
            else
                filter[pk.Key] = pk.Value?.ToString();
        }

        var res = await col.DeleteOneAsync(filter);
        return (int)res.DeletedCount;
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
