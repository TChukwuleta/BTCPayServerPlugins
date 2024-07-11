using BTCPayServer.Plugins.MassStoreGenerator.Data;
using Microsoft.EntityFrameworkCore;

namespace BTCPayServer.Plugins.MassStoreGenerator;

public class MassStoreGeneratorDbContext : DbContext
{
    private readonly bool _designTime;

    public MassStoreGeneratorDbContext(DbContextOptions<MassStoreGeneratorDbContext> options, bool designTime = false)
        : base(options)
    {
        _designTime = designTime;
    }

    public DbSet<Store> Stores { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("BTCPayServer.Plugins.MassStoreGenerator");
    }
}
