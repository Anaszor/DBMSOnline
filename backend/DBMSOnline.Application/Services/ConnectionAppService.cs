using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DBMSOnline.Application.DTOs;
using DBMSOnline.Application.Interfaces;
using DBMSOnline.Domain.Entities;
using DBMSOnline.Domain.Exceptions;
using DBMSOnline.Domain.Interfaces;

namespace DBMSOnline.Application.Services;

public class ConnectionAppService : IConnectionAppService
{
    private readonly ISavedConnectionRepository _repository;
    private readonly IEncryptionService _encryptionService;
    private readonly ISsrfGuard _ssrfGuard;
    private readonly IDbProviderFactory _providerFactory;
    private readonly IQuerySafetyService _querySafetyService;

    public ConnectionAppService(
        ISavedConnectionRepository repository,
        IEncryptionService encryptionService,
        ISsrfGuard ssrfGuard,
        IDbProviderFactory providerFactory,
        IQuerySafetyService querySafetyService)
    {
        _repository = repository;
        _encryptionService = encryptionService;
        _ssrfGuard = ssrfGuard;
        _providerFactory = providerFactory;
        _querySafetyService = querySafetyService;
    }

    public async Task<bool> TestConnectionAsync(ConnectionRequestDto request)
    {
        _ssrfGuard.ValidateHost(request.Host);
        string sanitizedCs = _querySafetyService.SanitizeConnectionString(request.CustomConnectionString, request.DatabaseType);

        var tempConn = new SavedConnection
        {
            DatabaseType = request.DatabaseType,
            AuthType = request.AuthType,
            Host = request.Host,
            Port = request.Port,
            DatabaseName = request.DatabaseName,
            Username = request.Username,
            IsReadOnly = request.IsReadOnly,
            EnforceSsl = request.EnforceSsl
        };

        var provider = _providerFactory.GetProvider(request.DatabaseType);
        return await provider.TestConnectionAsync(tempConn, request.Password, sanitizedCs);
    }

    public async Task<ConnectionResponseDto> SaveConnectionAsync(ConnectionRequestDto request)
    {
        _ssrfGuard.ValidateHost(request.Host);
        string sanitizedCs = _querySafetyService.SanitizeConnectionString(request.CustomConnectionString, request.DatabaseType);

        SavedConnection entity;
        bool isNew = false;
        if (request.Id.HasValue && request.Id.Value != Guid.Empty)
        {
            entity = await _repository.GetByIdAsync(request.Id.Value) 
                     ?? throw new DomainException("Connection profile not found.");
            entity.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            isNew = true;
            entity = new SavedConnection { Id = Guid.NewGuid(), CreatedAt = DateTime.UtcNow };
        }

        entity.Name = request.Name;
        entity.DatabaseType = request.DatabaseType;
        entity.AuthType = request.AuthType;
        entity.Host = request.Host;
        entity.Port = request.Port;
        entity.DatabaseName = request.DatabaseName;
        entity.Username = request.Username;
        entity.IsReadOnly = request.IsReadOnly;
        entity.EnforceSsl = request.EnforceSsl;

        if (!string.IsNullOrEmpty(request.Password))
        {
            entity.EncryptedPassword = _encryptionService.Encrypt(request.Password);
        }

        if (!string.IsNullOrEmpty(sanitizedCs))
        {
            entity.EncryptedConnectionString = _encryptionService.Encrypt(sanitizedCs);
        }

        if (isNew)
        {
            await _repository.AddAsync(entity);
        }
        else
        {
            await _repository.UpdateAsync(entity);
        }

        return MapToDto(entity);
    }

    public async Task<List<ConnectionResponseDto>> GetAllConnectionsAsync()
    {
        var connections = await _repository.GetAllAsync();
        return connections.Select(MapToDto).ToList();
    }

    public async Task<ConnectionResponseDto?> GetConnectionByIdAsync(Guid id)
    {
        var conn = await _repository.GetByIdAsync(id);
        return conn != null ? MapToDto(conn) : null;
    }

    public async Task<bool> DeleteConnectionAsync(Guid id)
    {
        var conn = await _repository.GetByIdAsync(id);
        if (conn == null) return false;

        await _repository.DeleteAsync(id);
        return true;
    }

    public async Task<(SavedConnection connection, string password, string connectionString)> ResolveConnectionCredentialsAsync(Guid id)
    {
        var conn = await _repository.GetByIdAsync(id)
            ?? throw new DomainException("Database connection profile not found.");

        string password = string.IsNullOrEmpty(conn.EncryptedPassword) 
            ? string.Empty 
            : _encryptionService.Decrypt(conn.EncryptedPassword);

        string cs = string.IsNullOrEmpty(conn.EncryptedConnectionString) 
            ? string.Empty 
            : _encryptionService.Decrypt(conn.EncryptedConnectionString);

        return (conn, password, cs);
    }

    private ConnectionResponseDto MapToDto(SavedConnection entity) => new()
    {
        Id = entity.Id,
        Name = entity.Name,
        DatabaseType = entity.DatabaseType,
        AuthType = entity.AuthType,
        Host = entity.Host,
        Port = entity.Port,
        DatabaseName = entity.DatabaseName,
        Username = entity.Username,
        CustomConnectionString = string.IsNullOrEmpty(entity.EncryptedConnectionString) 
            ? string.Empty 
            : _encryptionService.Decrypt(entity.EncryptedConnectionString),
        IsReadOnly = entity.IsReadOnly,
        EnforceSsl = entity.EnforceSsl,
        CreatedAt = entity.CreatedAt
    };
}
