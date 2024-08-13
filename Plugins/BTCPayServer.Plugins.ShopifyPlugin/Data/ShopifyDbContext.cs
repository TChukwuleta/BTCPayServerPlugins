using BTCPayServer.Plugins.BigCommercePlugin.Data;
using Microsoft.EntityFrameworkCore;

namespace BTCPayServer.Plugins.BigCommercePlugin;

public class ShopifyDbContext : DbContext
{
    private readonly bool _designTime;

    public ShopifyDbContext(DbContextOptions<ShopifyDbContext> options, bool designTime = false)
        : base(options)
    {
        _designTime = designTime;
    }

    /*public DbSet<BigCommerceStore> BigCommerceStores { get; set; }
    public DbSet<Transaction> Transactions { get; set; }*/

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("BTCPayServer.Plugins.Shopify");

        Transaction.OnModelCreating(modelBuilder);
        BigCommerceStore.OnModelCreating(modelBuilder);
    }
}
