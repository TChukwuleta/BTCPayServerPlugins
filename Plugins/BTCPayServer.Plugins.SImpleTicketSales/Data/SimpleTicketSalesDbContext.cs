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

    public DbSet<TicketSalesTransaction> TicketSalesTransactions { get; set; }
    public DbSet<TicketSalesEvent> TicketSalesEvents { get; set; }
    public DbSet<TicketSalesEventTicket> TicketSalesEventTickets { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("BTCPayServer.Plugins.Ghost");
        TicketSalesTransaction.OnModelCreating(modelBuilder);
    }
}
