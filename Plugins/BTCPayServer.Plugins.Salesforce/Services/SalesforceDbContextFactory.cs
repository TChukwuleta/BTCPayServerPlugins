using BTCPayServer.Abstractions.Contracts;
using BTCPayServer.Abstractions.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Options;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;
using System;

namespace BTCPayServer.Plugins.Salesforce.Services;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<SalesforceDbContext>
{
    public SalesforceDbContext CreateDbContext(string[] args)
    {
        var builder = new DbContextOptionsBuilder<SalesforceDbContext>();
        builder.UseNpgsql("User ID=postgres;Host=127.0.0.1;Port=39372;Database=designtimebtcpay");
        return new SalesforceDbContext(builder.Options, true);
    }
}

public class SalesforceDbContextFactory : BaseDbContextFactory<SalesforceDbContext>
{
    public SalesforceDbContextFactory(IOptions<DatabaseOptions> options) : base(options, "BTCPayServer.Plugins.Salesforce")
    {
    }
    public override SalesforceDbContext CreateContext(Action<NpgsqlDbContextOptionsBuilder> npgsqlOptionsAction = null)
    {
        var builder = new DbContextOptionsBuilder<SalesforceDbContext>();
        ConfigureBuilder(builder, npgsqlOptionsAction);
        return new SalesforceDbContext(builder.Options);
    }
}
