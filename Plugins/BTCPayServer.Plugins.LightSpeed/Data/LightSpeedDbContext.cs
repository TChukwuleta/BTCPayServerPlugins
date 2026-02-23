using Microsoft.EntityFrameworkCore;

namespace BTCPayServer.Plugins.LightSpeed.Data;

public class LightSpeedDbContext : DbContext
{
    // dotnet ef migrations add initialMigration -o Data/Migrations
    private readonly bool _designTime;

    public LightSpeedDbContext(DbContextOptions<LightSpeedDbContext> options, bool designTime = false)
        : base(options)
    {
        _designTime = designTime;
    }

    public DbSet<LightSpeedPayment> LightSpeedPayments { get; set; }
    public DbSet<LightspeedSettings> LightspeedSettings { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("BTCPayServer.Plugins.LightspeedHQ");
    }
}
