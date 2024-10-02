using BTCPayServer.Abstractions.Contracts;
using BTCPayServer.Abstractions.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Options;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;
using System;

namespace BTCPayServer.Plugins.MassStoreGenerator.Services;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<MassStoreGeneratorDbContext>
{
    public MassStoreGeneratorDbContext CreateDbContext(string[] args)
    {
        var builder = new DbContextOptionsBuilder<MassStoreGeneratorDbContext>();

        // FIXME: Somehow the DateTimeOffset column types get messed up when not using Postgres
        // https://docs.microsoft.com/en-us/ef/core/managing-schemas/migrations/providers?tabs=dotnet-core-cli
        builder.UseNpgsql("User ID=postgres;Host=127.0.0.1;Port=39372;Database=designtimebtcpay");

        return new MassStoreGeneratorDbContext(builder.Options, true);
    }
}

public class MassStoreGeneratorDbContextFactory : BaseDbContextFactory<MassStoreGeneratorDbContext>
{
    public MassStoreGeneratorDbContextFactory(IOptions<DatabaseOptions> options) : base(options, "BTCPayServer.Plugins.MassStoreGenerator")
    {
    }

    public override MassStoreGeneratorDbContext CreateContext(Action<NpgsqlDbContextOptionsBuilder> npgsqlOptionsAction = null)
    {
        var builder = new DbContextOptionsBuilder<MassStoreGeneratorDbContext>();
        ConfigureBuilder(builder, npgsqlOptionsAction);
        return new MassStoreGeneratorDbContext(builder.Options);
    }
}
