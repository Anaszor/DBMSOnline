using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DBMSOnline.Domain.Entities;

namespace DBMSOnline.Domain.Interfaces;

public interface ISavedConnectionRepository
{
    Task<List<SavedConnection>> GetAllAsync();
    Task<SavedConnection?> GetByIdAsync(Guid id);
    Task AddAsync(SavedConnection connection);
    Task UpdateAsync(SavedConnection connection);
    Task DeleteAsync(Guid id);
}
