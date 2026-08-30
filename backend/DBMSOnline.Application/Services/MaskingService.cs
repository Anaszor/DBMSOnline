using System;
using System.Collections.Generic;
using System.Linq;
using DBMSOnline.Application.DTOs;
using DBMSOnline.Application.Interfaces;

namespace DBMSOnline.Application.Services;

public class MaskingService : IMaskingService
{
    private static readonly HashSet<string> SensitiveKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "password", "pass", "pwd", "ssn", "socialsecurity",
        "creditcard", "credit_card", "card_number", "cvv",
        "secret", "private_key", "privatekey", "token",
        "api_key", "apikey", "auth_token"
    };

    public void MaskQueryResult(QueryResultDto result)
    {
        if (result.Columns == null || result.Rows == null || result.Rows.Count == 0)
            return;

        var sensitiveColumns = result.Columns
            .Where(col => SensitiveKeywords.Any(key => col.Contains(key, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        if (!sensitiveColumns.Any()) return;

        foreach (var row in result.Rows)
        {
            foreach (var col in sensitiveColumns)
            {
                if (row.ContainsKey(col) && row[col] != null)
                {
                    row[col] = "********";
                }
            }
        }
    }

    public void MaskDataPageResult(DataPageResultDto result)
    {
        if (result.Columns == null || result.Rows == null || result.Rows.Count == 0)
            return;

        var sensitiveColumns = result.Columns
            .Where(col => SensitiveKeywords.Any(key => col.ColumnName.Contains(key, StringComparison.OrdinalIgnoreCase)))
            .Select(col => col.ColumnName)
            .ToList();

        if (!sensitiveColumns.Any()) return;

        foreach (var row in result.Rows)
        {
            foreach (var col in sensitiveColumns)
            {
                if (row.ContainsKey(col) && row[col] != null)
                {
                    row[col] = "********";
                }
            }
        }
    }
}
