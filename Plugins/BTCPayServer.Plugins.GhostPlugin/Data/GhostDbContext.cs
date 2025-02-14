using BTCPayServer.Plugins.GhostPlugin.Data;
using Microsoft.EntityFrameworkCore;

namespace BTCPayServer.Plugins.GhostPlugin;

public class GhostDbContext : DbContext
{
    // dotnet ef migrations add initialMigration -o Data/Migrations
    private readonly bool _designTime;

    public GhostDbContext(DbContextOptions<GhostDbContext> options, bool designTime = false)
        : base(options)
    {
        _designTime = designTime;
    }

    public DbSet<GhostSetting> GhostSettings { get; set; }
    public DbSet<GhostMember> GhostMembers { get; set; }
    public DbSet<GhostTransaction> GhostTransactions { get; set; }
    public DbSet<GhostEvent> GhostEvents { get; set; }
    public DbSet<GhostEventTicket> GhostEventTickets { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("BTCPayServer.Plugins.Ghost");
        GhostSetting.OnModelCreating(modelBuilder);
        GhostMember.OnModelCreating(modelBuilder);
        GhostTransaction.OnModelCreating(modelBuilder);
    }
}
