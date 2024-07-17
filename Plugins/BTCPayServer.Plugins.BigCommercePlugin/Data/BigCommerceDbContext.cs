using BTCPayServer.Plugins.BigCommercePlugin.Data;
using Microsoft.EntityFrameworkCore;

namespace BTCPayServer.Plugins.BigCommercePlugin;

public class BigCommerceDbContext : DbContext
{
    private readonly bool _designTime;

    public BigCommerceDbContext(DbContextOptions<BigCommerceDbContext> options, bool designTime = false)
        : base(options)
    {
        _designTime = designTime;
    }

    public DbSet<BigCommerceStore> BigCommerceStores { get; set; }
    public DbSet<Transaction> Transactions { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("BTCPayServer.Plugins.BigCommerce");
    }
}
