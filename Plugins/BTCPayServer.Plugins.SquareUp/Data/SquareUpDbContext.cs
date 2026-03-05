using BTCPayServer.Plugins.SquareUp.Data;
using Microsoft.EntityFrameworkCore;

namespace BTCPayServer.Plugins.JumpSeller.Data;

public class SquareUpDbContext : DbContext
{
    // dotnet ef migrations add initialMigration -o Data/Migrations
    private readonly bool _designTime;

    public SquareUpDbContext(DbContextOptions<SquareUpDbContext> options, bool designTime = false)
        : base(options)
    {
        _designTime = designTime;
    }

    public DbSet<SquareUpOrderMapping> SquareUpOrderMappings { get; set; }
    public DbSet<SquareUpStoreSetting> SquareUpStoreSettings { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("BTCPayServer.Plugins.SquareUp");
    }
}
