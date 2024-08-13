using BTCPayServer.Plugins.BigCommercePlugin.Data;
using BTCPayServer.Plugins.ShopifyPlugin.Data;
using Microsoft.EntityFrameworkCore;

namespace BTCPayServer.Plugins.ShopifyPlugin;

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
    public DbSet<ShopifySetting> ShopifySettings { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("BTCPayServer.Plugins.Shopify");

        Transaction.OnModelCreating(modelBuilder);
        ShopifySetting.OnModelCreating(modelBuilder);
    }
}
