using System;
using BTCPayServer.Abstractions.Contracts;
using BTCPayServer.Abstractions.Models;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;

namespace BTCPayServer.Plugins.NairaCheckout.Services;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<NairaCheckoutDbContext>
{
    public NairaCheckoutDbContext CreateDbContext(string[] args)
    {
        var builder = new DbContextOptionsBuilder<NairaCheckoutDbContext>();
        builder.UseNpgsql("User ID=postgres;Host=127.0.0.1;Port=39372;Database=designtimebtcpay");
        return new NairaCheckoutDbContext(builder.Options, true);
    }
}

public class NairaCheckoutDbContextFactory : BaseDbContextFactory<NairaCheckoutDbContext>
{
    public NairaCheckoutDbContextFactory(IOptions<DatabaseOptions> options) : base(options, "BTCPayServer.Plugins.NairaCheckout")
    {
    }

    public override NairaCheckoutDbContext CreateContext(Action<NpgsqlDbContextOptionsBuilder> npgsqlOptionsAction = null)
    {
        var builder = new DbContextOptionsBuilder<NairaCheckoutDbContext>();
        ConfigureBuilder(builder, npgsqlOptionsAction);
        return new NairaCheckoutDbContext(builder.Options);
    }
}
