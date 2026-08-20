using BTCPayServer.Plugins.SatoshiTickets.Data;
using Microsoft.EntityFrameworkCore;

namespace BTCPayServer.Plugins.SatoshiTickets;

public class SimpleTicketSalesDbContext : DbContext
{
    // dotnet ef migrations add initialMigration -o Data/Migrations
    private readonly bool _designTime;

    public SimpleTicketSalesDbContext(DbContextOptions<SimpleTicketSalesDbContext> options, bool designTime = false)
        : base(options)
    {
        _designTime = designTime;
    }

    public DbSet<Order> Orders { get; set; }
    public DbSet<Event> Events { get; set; }
    public DbSet<Ticket> Tickets { get; set; }
    public DbSet<TicketType> TicketTypes { get; set; }
    public DbSet<DiscountCode> DiscountCodes { get; set; }
    public DbSet<Referrer> Referrers { get; set; }
    public DbSet<ReferrerInvitation> ReferrerInvitations { get; set; }
    public DbSet<ReferralPayout> ReferralPayouts { get; set; }
    public DbSet<ReferralCredit> ReferralCredits { get; set; }
    public DbSet<SatoshiTicketsSetting> SatoshiTicketsSettings { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("BTCPayServer.Plugins.SatoshiTickets");

        modelBuilder.Entity<DiscountCode>().HasIndex(d => new { d.EventId, d.Code }).IsUnique();

        modelBuilder.Entity<Referrer>().HasIndex(r => new { r.StoreId, r.Email }).IsUnique();
        
        modelBuilder.Entity<ReferrerInvitation>().HasIndex(i => i.Token);
        modelBuilder.Entity<ReferralCredit>().HasIndex(c => c.OrderId).IsUnique();
        modelBuilder.Entity<ReferralCredit>().HasIndex(c => new { c.ReferrerId, c.Status });
    }
}
