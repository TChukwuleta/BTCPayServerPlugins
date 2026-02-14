using System;
using BTCPayServer.Abstractions.Contracts;
using BTCPayServer.Abstractions.Models;
using BTCPayServer.Plugins.SquareSpace.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Options;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;

namespace BTCPayServer.Plugins.SquareSpace.Services;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<SquareSpaceDbContext>
{
    public SquareSpaceDbContext CreateDbContext(string[] args)
    {
        var builder = new DbContextOptionsBuilder<SquareSpaceDbContext>();

        // FIXME: Somehow the DateTimeOffset column types get messed up when not using Postgres
        // https://docs.microsoft.com/en-us/ef/core/managing-schemas/migrations/providers?tabs=dotnet-core-cli
        builder.UseNpgsql("User ID=postgres;Host=127.0.0.1;Port=39372;Database=designtimebtcpay");

        return new SquareSpaceDbContext(builder.Options, true);
    }
}

public class SquareSpaceDbContextFactory : BaseDbContextFactory<SquareSpaceDbContext>
{
    public SquareSpaceDbContextFactory(IOptions<DatabaseOptions> options) : base(options, "BTCPayServer.Plugins.SquareSpace")
    {
    }

    public override SquareSpaceDbContext CreateContext(Action<NpgsqlDbContextOptionsBuilder> npgsqlOptionsAction = null)
    {
        var builder = new DbContextOptionsBuilder<SquareSpaceDbContext>();
        ConfigureBuilder(builder, npgsqlOptionsAction);
        return new SquareSpaceDbContext(builder.Options);
    }
}