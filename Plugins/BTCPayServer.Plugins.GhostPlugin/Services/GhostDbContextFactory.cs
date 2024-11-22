using BTCPayServer.Abstractions.Contracts;
using BTCPayServer.Abstractions.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Options;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;
using System;

namespace BTCPayServer.Plugins.GhostPlugin.Services;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<GhostDbContext>
{
    public GhostDbContext CreateDbContext(string[] args)
    {
        var builder = new DbContextOptionsBuilder<GhostDbContext>();

        // FIXME: Somehow the DateTimeOffset column types get messed up when not using Postgres
        // https://docs.microsoft.com/en-us/ef/core/managing-schemas/migrations/providers?tabs=dotnet-core-cli
        builder.UseNpgsql("User ID=postgres;Host=127.0.0.1;Port=39372;Database=designtimebtcpay");

        return new GhostDbContext(builder.Options, true);
    }
}

public class GhostDbContextFactory : BaseDbContextFactory<GhostDbContext>
{
    public GhostDbContextFactory(IOptions<DatabaseOptions> options) : base(options, "BTCPayServer.Plugins.Ghost")
    {
    }

    public override GhostDbContext CreateContext(Action<NpgsqlDbContextOptionsBuilder> npgsqlOptionsAction = null)
    {
        var builder = new DbContextOptionsBuilder<GhostDbContext>();
        ConfigureBuilder(builder, npgsqlOptionsAction);
        return new GhostDbContext(builder.Options);
    }
}
