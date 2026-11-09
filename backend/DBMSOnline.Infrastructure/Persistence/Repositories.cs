using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using DBMSOnline.Domain.Entities;
using DBMSOnline.Domain.Interfaces;

namespace DBMSOnline.Infrastructure.Persistence;

public class AuditLogRepository : IAuditLogRepository
{
    private readonly AppDbContext _context;

    public AuditLogRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<AuditLog>> GetAllAsync()
    {
        return await _context.AuditLogs.AsNoTracking().ToListAsync();
    }

    public async Task AddAsync(AuditLog log)
    {
        await _context.AuditLogs.AddAsync(log);
        await _context.SaveChangesAsync();
    }
}

public class ReportRepository : IReportRepository
{
    private readonly AppDbContext _context;

    public ReportRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<SavedReport>> GetAllAsync()
    {
        return await _context.SavedReports.AsNoTracking().OrderByDescending(r => r.CreatedAt).ToListAsync();
    }

    public async Task<SavedReport?> GetByIdAsync(Guid id)
    {
        return await _context.SavedReports.FindAsync(id);
    }

    public async Task AddAsync(SavedReport report)
    {
        await _context.SavedReports.AddAsync(report);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(SavedReport report)
    {
        _context.SavedReports.Update(report);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(SavedReport report)
    {
        _context.SavedReports.Remove(report);
        await _context.SaveChangesAsync();
    }
}

public class DatabaseBackupRepository : IDatabaseBackupRepository
{
    private readonly AppDbContext _context;

    public DatabaseBackupRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<DatabaseBackup>> GetByConnectionIdAsync(Guid connectionId)
    {
        return await _context.DatabaseBackups
            .AsNoTracking()
            .Where(b => b.ConnectionId == connectionId)
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync();
    }

    public async Task<DatabaseBackup?> GetByIdAsync(Guid id)
    {
        return await _context.DatabaseBackups.FindAsync(id);
    }

    public async Task AddAsync(DatabaseBackup backup)
    {
        await _context.DatabaseBackups.AddAsync(backup);
        await _context.SaveChangesAsync();
    }
}
