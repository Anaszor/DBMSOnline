using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using DBMSOnline.Domain.Entities;
using DBMSOnline.Domain.Interfaces;

namespace DBMSOnline.Infrastructure.Persistence;

public class SavedConnectionRepository : ISavedConnectionRepository
{
    private readonly AppDbContext _context;

    public SavedConnectionRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<SavedConnection>> GetAllAsync()
    {
        return await _context.SavedConnections
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();
    }

    public async Task<SavedConnection?> GetByIdAsync(Guid id)
    {
        return await _context.SavedConnections.FindAsync(id);
    }

    public async Task AddAsync(SavedConnection connection)
    {
        _context.SavedConnections.Add(connection);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(SavedConnection connection)
    {
        _context.SavedConnections.Update(connection);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        var conn = await _context.SavedConnections.FindAsync(id);
        if (conn != null)
        {
            _context.SavedConnections.Remove(conn);
            await _context.SaveChangesAsync();
        }
    }
}
