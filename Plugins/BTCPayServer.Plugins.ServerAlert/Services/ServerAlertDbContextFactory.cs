using System;
using BTCPayServer.Abstractions.Contracts;
using BTCPayServer.Abstractions.Models;
using BTCPayServer.Plugins.LightSpeed.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Options;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;

namespace BTCPayServer.Plugins.ServerAlert.Services;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ServerAlertDbContext>
{
    public ServerAlertDbContext CreateDbContext(string[] args)
    {
        var builder = new DbContextOptionsBuilder<ServerAlertDbContext>();

        // FIXME: Somehow the DateTimeOffset column types get messed up when not using Postgres
        // https://docs.microsoft.com/en-us/ef/core/managing-schemas/migrations/providers?tabs=dotnet-core-cli
        builder.UseNpgsql("User ID=postgres;Host=127.0.0.1;Port=39372;Database=designtimebtcpay");

        return new ServerAlertDbContext(builder.Options, true);
    }
}

public class ServerAlertDbContextFactory : BaseDbContextFactory<ServerAlertDbContext>
{
    public ServerAlertDbContextFactory(IOptions<DatabaseOptions> options) : base(options, "BTCPayServer.Plugins.ServerAlert")
    {
    }

    public override ServerAlertDbContext CreateContext(Action<NpgsqlDbContextOptionsBuilder> npgsqlOptionsAction = null)
    {
        var builder = new DbContextOptionsBuilder<ServerAlertDbContext>();
        ConfigureBuilder(builder, npgsqlOptionsAction);
        return new ServerAlertDbContext(builder.Options);
    }
}