using BTCPayServer.Abstractions.Contracts;
using BTCPayServer.Abstractions.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Options;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;
using System;

namespace BTCPayServer.Plugins.SimpleTicketSales.Services;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<SimpleTicketSalesDbContext>
{
    public SimpleTicketSalesDbContext CreateDbContext(string[] args)
    {
        var builder = new DbContextOptionsBuilder<SimpleTicketSalesDbContext>();

        // FIXME: Somehow the DateTimeOffset column types get messed up when not using Postgres
        // https://docs.microsoft.com/en-us/ef/core/managing-schemas/migrations/providers?tabs=dotnet-core-cli
        builder.UseNpgsql("User ID=postgres;Host=127.0.0.1;Port=39372;Database=designtimebtcpay");

        return new SimpleTicketSalesDbContext(builder.Options, true);
    }
}

public class SimpleTicketSalesDbContextFactory : BaseDbContextFactory<SimpleTicketSalesDbContext>
{
    public SimpleTicketSalesDbContextFactory(IOptions<DatabaseOptions> options) : base(options, "BTCPayServer.Plugins.SimpleTicketSale")
    {
    }

    public override SimpleTicketSalesDbContext CreateContext(Action<NpgsqlDbContextOptionsBuilder> npgsqlOptionsAction = null)
    {
        var builder = new DbContextOptionsBuilder<SimpleTicketSalesDbContext>();
        ConfigureBuilder(builder, npgsqlOptionsAction);
        return new SimpleTicketSalesDbContext(builder.Options);
    }
}
