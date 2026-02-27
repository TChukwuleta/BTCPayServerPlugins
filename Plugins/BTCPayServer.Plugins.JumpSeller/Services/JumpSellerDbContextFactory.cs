using System;
using BTCPayServer.Abstractions.Contracts;
using BTCPayServer.Abstractions.Models;
using BTCPayServer.Plugins.JumpSeller.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Options;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;

namespace BTCPayServer.Plugins.JumpSeller.Services;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<JumpSellerDbContext>
{
    public JumpSellerDbContext CreateDbContext(string[] args)
    {
        var builder = new DbContextOptionsBuilder<JumpSellerDbContext>();

        // FIXME: Somehow the DateTimeOffset column types get messed up when not using Postgres
        // https://docs.microsoft.com/en-us/ef/core/managing-schemas/migrations/providers?tabs=dotnet-core-cli
        builder.UseNpgsql("User ID=postgres;Host=127.0.0.1;Port=39372;Database=designtimebtcpay");

        return new JumpSellerDbContext(builder.Options, true);
    }
}

public class JumpSellerDbContextFactory : BaseDbContextFactory<JumpSellerDbContext>
{
    public JumpSellerDbContextFactory(IOptions<DatabaseOptions> options) : base(options, "BTCPayServer.Plugins.JumpSeller")
    {
    }

    public override JumpSellerDbContext CreateContext(Action<NpgsqlDbContextOptionsBuilder> npgsqlOptionsAction = null)
    {
        var builder = new DbContextOptionsBuilder<JumpSellerDbContext>();
        ConfigureBuilder(builder, npgsqlOptionsAction);
        return new JumpSellerDbContext(builder.Options);
    }
}