using BTCPayServer.Plugins.JumpSeller.Data;
using Microsoft.EntityFrameworkCore;

namespace BTCPayServer.Plugins.JumpSeller.Data;

public class JumpSellerDbContext : DbContext
{
    // dotnet ef migrations add initialMigration -o Data/Migrations
    private readonly bool _designTime;

    public JumpSellerDbContext(DbContextOptions<JumpSellerDbContext> options, bool designTime = false)
        : base(options)
    {
        _designTime = designTime;
    }

    public DbSet<JumpSellerInvoice> JumpSellerInvoices { get; set; }
    public DbSet<JumpSellerStoreSetting> JumpSellerStoreSettings { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("BTCPayServer.Plugins.JumpSeller");
    }
}
