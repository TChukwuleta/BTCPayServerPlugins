using System;
using BTCPayServer.Abstractions.Contracts;
using BTCPayServer.Abstractions.Models;
using BTCPayServer.Plugins.LightSpeed.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Options;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;

namespace BTCPayServer.Plugins.LightSpeed.Services;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<LightSpeedDbContext>
{
    public LightSpeedDbContext CreateDbContext(string[] args)
    {
        var builder = new DbContextOptionsBuilder<LightSpeedDbContext>();

        // FIXME: Somehow the DateTimeOffset column types get messed up when not using Postgres
        // https://docs.microsoft.com/en-us/ef/core/managing-schemas/migrations/providers?tabs=dotnet-core-cli
        builder.UseNpgsql("User ID=postgres;Host=127.0.0.1;Port=39372;Database=designtimebtcpay");

        return new LightSpeedDbContext(builder.Options, true);
    }
}

public class LightSpeedDbContextFactory : BaseDbContextFactory<LightSpeedDbContext>
{
    public LightSpeedDbContextFactory(IOptions<DatabaseOptions> options) : base(options, "BTCPayServer.Plugins.LightspeedHQ")
    {
    }

    public override LightSpeedDbContext CreateContext(Action<NpgsqlDbContextOptionsBuilder> npgsqlOptionsAction = null)
    {
        var builder = new DbContextOptionsBuilder<LightSpeedDbContext>();
        ConfigureBuilder(builder, npgsqlOptionsAction);
        return new LightSpeedDbContext(builder.Options);
    }
}