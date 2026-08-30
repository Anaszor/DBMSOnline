using System;
using System.Collections.Generic;
using System.Linq;
using DBMSOnline.Application.Interfaces;
using DBMSOnline.Domain.Enums;

namespace DBMSOnline.Infrastructure.DbProviders;

public class DbProviderFactory : IDbProviderFactory
{
    private readonly IEnumerable<IDbProvider> _providers;

    public DbProviderFactory(IEnumerable<IDbProvider> providers)
    {
        _providers = providers;
    }

    public IDbProvider GetProvider(DatabaseType dbType)
    {
        var provider = _providers.FirstOrDefault(p => p.DatabaseType == dbType);
        if (provider == null)
            throw new NotSupportedException($"Database provider '{dbType}' is not registered or supported.");

        return provider;
    }
}
