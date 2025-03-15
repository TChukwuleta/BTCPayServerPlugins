using BTCPayServer.Plugins.SimpleTicketSales.Data;
using Microsoft.EntityFrameworkCore;

namespace BTCPayServer.Plugins.SimpleTicketSales;

public class SimpleTicketSalesDbContext : DbContext
{
    // dotnet ef migrations add initialMigration -o Data/Migrations
    private readonly bool _designTime;

    public SimpleTicketSalesDbContext(DbContextOptions<SimpleTicketSalesDbContext> options, bool designTime = false)
        : base(options)
    {
        _designTime = designTime;
    }

    public DbSet<Event> Events { get; set; }
    public DbSet<Ticket> Tickets { get; set; }
    public DbSet<TicketType> TicketTypes { get; set; }
    public DbSet<Order> Orders { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("BTCPayServer.Plugins.SimpleTicketSale");
    }
}
