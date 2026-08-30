using System;
using System.Threading.Tasks;
using DBMSOnline.Domain.Entities;
using DBMSOnline.Domain.Interfaces;

namespace DBMSOnline.Infrastructure.Persistence;

public class AuditLogger : IAuditLogger
{
    private readonly AppDbContext _context;

    public AuditLogger(AppDbContext context)
    {
        _context = context;
    }

    public async Task LogAsync(AuditLog log)
    {
        try
        {
            _context.AuditLogs.Add(log);
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            // Fallback to console / event stream if DB audit logging fails
            Console.WriteLine($"[AUDIT FAILURE] Failed to write audit log: {ex.Message}");
        }
    }
}
