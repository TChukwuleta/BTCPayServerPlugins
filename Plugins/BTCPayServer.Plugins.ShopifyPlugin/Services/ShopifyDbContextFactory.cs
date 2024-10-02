using BTCPayServer.Abstractions.Contracts;
using BTCPayServer.Abstractions.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Options;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;
using System;

namespace BTCPayServer.Plugins.ShopifyPlugin.Services;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ShopifyDbContext>
{
    public ShopifyDbContext CreateDbContext(string[] args)
    {
        var builder = new DbContextOptionsBuilder<ShopifyDbContext>();

        // FIXME: Somehow the DateTimeOffset column types get messed up when not using Postgres
        // https://docs.microsoft.com/en-us/ef/core/managing-schemas/migrations/providers?tabs=dotnet-core-cli
        builder.UseNpgsql("User ID=postgres;Host=127.0.0.1;Port=39372;Database=designtimebtcpay");

        return new ShopifyDbContext(builder.Options, true);
    }
}

public class ShopifyDbContextFactory : BaseDbContextFactory<ShopifyDbContext>
{
    public ShopifyDbContextFactory(IOptions<DatabaseOptions> options) : base(options, "BTCPayServer.Plugins.Shopify")
    {
    }

    public override ShopifyDbContext CreateContext(Action<NpgsqlDbContextOptionsBuilder> npgsqlOptionsAction = null)
    {
        var builder = new DbContextOptionsBuilder<ShopifyDbContext>();
        ConfigureBuilder(builder, npgsqlOptionsAction);
        return new ShopifyDbContext(builder.Options);
    }
}
