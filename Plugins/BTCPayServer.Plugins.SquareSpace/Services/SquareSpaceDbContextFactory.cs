using System;
using BTCPayServer.Abstractions.Contracts;
using BTCPayServer.Abstractions.Models;
using BTCPayServer.Plugins.StoreBridge.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Options;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;

namespace BTCPayServer.Plugins.StoreBridge.Services;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<StoreBridgeDbContext>
{
    public StoreBridgeDbContext CreateDbContext(string[] args)
    {
        var builder = new DbContextOptionsBuilder<StoreBridgeDbContext>();

        // FIXME: Somehow the DateTimeOffset column types get messed up when not using Postgres
        // https://docs.microsoft.com/en-us/ef/core/managing-schemas/migrations/providers?tabs=dotnet-core-cli
        builder.UseNpgsql("User ID=postgres;Host=127.0.0.1;Port=39372;Database=designtimebtcpay");

        return new StoreBridgeDbContext(builder.Options, true);
    }
}

public class SquareSpaceDbContextFactory : BaseDbContextFactory<StoreBridgeDbContext>
{
    public SquareSpaceDbContextFactory(IOptions<DatabaseOptions> options) : base(options, "BTCPayServer.Plugins.StoreBridge")
    {
    }

    public override StoreBridgeDbContext CreateContext(Action<NpgsqlDbContextOptionsBuilder> npgsqlOptionsAction = null)
    {
        var builder = new DbContextOptionsBuilder<StoreBridgeDbContext>();
        ConfigureBuilder(builder, npgsqlOptionsAction);
        return new StoreBridgeDbContext(builder.Options);
    }
}