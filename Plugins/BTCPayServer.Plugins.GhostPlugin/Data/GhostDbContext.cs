using BTCPayServer.Plugins.GhostPlugin.Data;
using Microsoft.EntityFrameworkCore;

namespace BTCPayServer.Plugins.GhostPlugin;

public class GhostDbContext : DbContext
{
    private readonly bool _designTime;

    public GhostDbContext(DbContextOptions<GhostDbContext> options, bool designTime = false)
        : base(options)
    {
        _designTime = designTime;
    }

    public DbSet<GhostSetting> GhostSettings { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("BTCPayServer.Plugins.Ghost");

        GhostSetting.OnModelCreating(modelBuilder);
    }
}
