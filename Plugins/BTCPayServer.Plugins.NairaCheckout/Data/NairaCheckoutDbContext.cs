using BTCPayServer.Plugins.NairaCheckout.Data;
using Microsoft.EntityFrameworkCore;

namespace BTCPayServer.Plugins.NairaCheckout;

public class NairaCheckoutDbContext : DbContext
{
    // dotnet ef migrations add initialMigration -o Data/Migrations
    private readonly bool _designTime;

    public NairaCheckoutDbContext(DbContextOptions<NairaCheckoutDbContext> options, bool designTime = false)
        : base(options)
    {
        _designTime = designTime;
    }

    public DbSet<MavapaySetting> MavapaySettings { get; set; }
    public DbSet<NairaCheckoutSetting> NairaCheckoutSettings { get; set; }

}
