using BTCPayServer.Plugins.Salesforce.Data;
using Microsoft.EntityFrameworkCore;

namespace BTCPayServer.Plugins.Salesforce;

public class SalesforceDbContext : DbContext
{
    private readonly bool _designTime;

    public SalesforceDbContext(DbContextOptions<SalesforceDbContext> options, bool designTime = false)
        : base(options)
    {
        _designTime = designTime;
    }

    public DbSet<SalesforceSetting> SalesforceSettings { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("BTCPayServer.Plugins.Salesforce");
    }
}
