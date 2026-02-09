using Microsoft.EntityFrameworkCore;

namespace BTCPayServer.Plugins.StoreBridge.Data;

public class StoreBridgeDbContext : DbContext
{
    // dotnet ef migrations add initialMigration -o Data/Migrations
    private readonly bool _designTime;

    public StoreBridgeDbContext(DbContextOptions<StoreBridgeDbContext> options, bool designTime = false)
        : base(options)
    {
        _designTime = designTime;
    }

    public DbSet<TemplateData> StoreBridgeTemplates { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("BTCPayServer.Plugins.StoreBridge");
    }
}
