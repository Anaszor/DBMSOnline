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

public class AuthAppService : IAuthAppService
{
    private readonly IUserRepository _userRepository;
    private readonly ITenantRepository _tenantRepository;

    public AuthAppService(IUserRepository userRepository, ITenantRepository tenantRepository)
    {
        _userRepository = userRepository;
        _tenantRepository = tenantRepository;
    }

    public async Task<LoginResponseDto> LoginAsync(LoginRequestDto request)
    {
        var user = await _userRepository.GetByEmailAsync(request.Email);

        if (user == null || !user.IsActive || user.PasswordHash != request.Password)
        {
            return new LoginResponseDto
            {
                Success = false,
                Message = "Invalid email or password."
            };
        }

        user.LastLoginAt = DateTime.UtcNow;
        await _userRepository.UpdateAsync(user);

        var tenant = await _tenantRepository.GetByIdAsync(user.TenantId);

        return new LoginResponseDto
        {
            Success = true,
            Message = "Authentication successful.",
            Token = $"Bearer_Enterprise_{Guid.NewGuid()}",
            User = MapUserToDto(user),
            Tenant = tenant != null ? MapTenantToDto(tenant, 1) : null
        };
    }

    public async Task<UserDto> CreateUserAsync(CreateUserRequestDto request)
    {
        var existing = await _userRepository.GetByEmailAsync(request.Email);
        if (existing != null)
        {
            throw new DomainException($"User with email '{request.Email}' already exists.");
        }

        var user = new User
        {
            TenantId = request.TenantId,
            Email = request.Email,
            PasswordHash = request.Password,
            FullName = request.FullName,
            Role = request.Role,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        await _userRepository.AddAsync(user);

        return MapUserToDto(user);
    }

    public async Task<TenantDto> CreateTenantAsync(CreateTenantRequestDto request)
    {
        var tenant = new Tenant
        {
            Name = request.Name,
            Domain = request.Domain,
            PlanType = request.PlanType,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        await _tenantRepository.AddAsync(tenant);

        var adminUser = new User
        {
            TenantId = tenant.Id,
            Email = request.AdminEmail,
            PasswordHash = request.AdminPassword,
            FullName = request.AdminFullName,
            Role = Domain.Enums.UserRole.OrgAdmin,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        await _userRepository.AddAsync(adminUser);

        return MapTenantToDto(tenant, 1);
    }

    public async Task<List<UserDto>> GetTenantUsersAsync(Guid tenantId)
    {
        var users = await _userRepository.GetByTenantIdAsync(tenantId);
        return users.Select(MapUserToDto).ToList();
    }

    public async Task<List<TenantDto>> GetAllTenantsAsync()
    {
        var tenants = await _tenantRepository.GetAllAsync();
        var result = new List<TenantDto>();

        foreach (var t in tenants)
        {
            var users = await _userRepository.GetByTenantIdAsync(t.Id);
            result.Add(MapTenantToDto(t, users.Count));
        }

        return result;
    }

    private static UserDto MapUserToDto(User u)
    {
        return new UserDto
        {
            Id = u.Id,
            TenantId = u.TenantId,
            Email = u.Email,
            FullName = u.FullName,
            Role = u.Role,
            IsActive = u.IsActive,
            CreatedAt = u.CreatedAt
        };
    }

    private static TenantDto MapTenantToDto(Tenant t, int userCount)
    {
        return new TenantDto
        {
            Id = t.Id,
            Name = t.Name,
            Domain = t.Domain,
            PlanType = t.PlanType,
            IsActive = t.IsActive,
            UserCount = userCount,
            CreatedAt = t.CreatedAt
        };
    }
}
