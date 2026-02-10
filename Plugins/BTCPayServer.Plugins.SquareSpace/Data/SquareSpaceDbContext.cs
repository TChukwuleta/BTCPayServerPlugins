using Microsoft.EntityFrameworkCore;

namespace BTCPayServer.Plugins.SquareSpace.Data;

public class SquareSpaceDbContext : DbContext
{
    // dotnet ef migrations add initialMigration -o Data/Migrations
    private readonly bool _designTime;

    public SquareSpaceDbContext(DbContextOptions<SquareSpaceDbContext> options, bool designTime = false)
        : base(options)
    {
        _designTime = designTime;
    }

    public DbSet<SquareSpaceOrder> SquareSpaceOrders { get; set; }
    public DbSet<SquareSpaceSetting> SquareSpaceSettings { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("BTCPayServer.Plugins.SquareSpace");
    }
}
