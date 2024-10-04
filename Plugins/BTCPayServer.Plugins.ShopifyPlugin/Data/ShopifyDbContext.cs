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

    public DbSet<ShopifySetting> ShopifySettings { get; set; }
    public DbSet<Order> Orders { get; set; }
    public DbSet<Transaction> Transactions { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("BTCPayServer.Plugins.Shopify");
        
        ShopifySetting.OnModelCreating(modelBuilder);
    }
}
