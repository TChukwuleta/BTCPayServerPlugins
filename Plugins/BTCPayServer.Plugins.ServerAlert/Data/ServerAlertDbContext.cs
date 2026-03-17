using BTCPayServer.Plugins.ServerAlert.Data;
using Microsoft.EntityFrameworkCore;

namespace BTCPayServer.Plugins.LightSpeed.Data;

public class ServerAlertDbContext : DbContext
{
    // dotnet ef migrations add initialMigration -o Data/Migrations
    private readonly bool _designTime;

    public ServerAlertDbContext(DbContextOptions<ServerAlertDbContext> options, bool designTime = false)
        : base(options)
    {
        _designTime = designTime;
    }

    public DbSet<Announcement> Announcements { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("BTCPayServer.Plugins.ServerAlert");

        modelBuilder.Entity<Announcement>(e =>
        {
            e.HasKey(a => a.Id);
            e.Property(a => a.Title).HasMaxLength(200).IsRequired();
            e.Property(a => a.Message).IsRequired();
            e.Property(a => a.Severity).HasConversion<int>();
            e.Property(a => a.EmailScope).HasConversion<int>();
            e.HasIndex(a => new { a.IsPublished, a.CreatedAt });
        });
    }
}
