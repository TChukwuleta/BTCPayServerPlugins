using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BTCPayServer.Plugins.SatoshiTickets.Data;
using Microsoft.EntityFrameworkCore;

namespace BTCPayServer.Plugins.SatoshiTickets.Services;

public record DiscountCartLine(string TicketTypeId, decimal UnitPrice, int Quantity);

public class DiscountApplication
{
    public DiscountValidationStatus Status { get; init; }
    public bool IsValid => Status == DiscountValidationStatus.Valid;

    public DiscountCode Code { get; init; }
    public decimal Subtotal { get; init; }
    public decimal DiscountAmount { get; init; }
    public decimal Total => Subtotal - DiscountAmount;
    public string ErrorMessage { get; init; }

    public static DiscountApplication Invalid(DiscountValidationStatus status, decimal subtotal, string message) =>
        new() { Status = status, Subtotal = subtotal, DiscountAmount = 0m, ErrorMessage = message };
}

public class DiscountCodeService(SimpleTicketSalesDbContextFactory dbContextFactory)
{
    private const decimal MinimumPayableTotal = 0.01m;

    public static string Normalize(string code) => code?.Trim().ToUpperInvariant() ?? string.Empty;

    public async Task<DiscountApplication> Evaluate(string storeId, string eventId, string rawCode, IReadOnlyList<DiscountCartLine> cart)
    {
        var subtotal = cart.Sum(l => l.UnitPrice * l.Quantity);
        var normalized = Normalize(rawCode);

        if (string.IsNullOrEmpty(normalized))
            return DiscountApplication.Invalid(DiscountValidationStatus.NotFound, subtotal, "Enter a discount code");

        await using var ctx = dbContextFactory.CreateContext();
        var code = await ctx.DiscountCodes.AsNoTracking()
            .FirstOrDefaultAsync(d => d.StoreId == storeId && d.EventId == eventId && d.Code == normalized);

        return Evaluate(code, cart, subtotal);
    }

    private static DiscountApplication Evaluate(DiscountCode code, IReadOnlyList<DiscountCartLine> cart, decimal subtotal)
    {
        if (code == null)
            return DiscountApplication.Invalid(DiscountValidationStatus.NotFound, subtotal, "That discount code wasn't found");

        if (code.DiscountCodeState != DiscountCodeState.Active)
            return DiscountApplication.Invalid(DiscountValidationStatus.Inactive, subtotal, "That discount code is no longer active");

        if (code.ExpiryDate.HasValue && code.ExpiryDate.Value < DateTimeOffset.UtcNow)
            return DiscountApplication.Invalid(DiscountValidationStatus.Expired, subtotal, "That discount code has expired");

        if (code.MaxUses.HasValue && code.UsesCount >= code.MaxUses.Value)
            return DiscountApplication.Invalid(DiscountValidationStatus.MaxUsesReached, subtotal, "That discount code has reached its usage limit");

        // Determine which cart lines the discount applies to.
        // TicketTypeId == null => whole cart. Otherwise => only matching lines.
        var eligibleLines = code.TicketTypeId == null ? cart : cart.Where(l => l.TicketTypeId == code.TicketTypeId).ToList();

        var eligibleQuantity = eligibleLines.Sum(l => l.Quantity);
        if (eligibleQuantity == 0)
            return DiscountApplication.Invalid(DiscountValidationStatus.NotApplicableToCart, subtotal, "This code doesn't apply to any tickets in your cart");

        if (code.MinQuantity.HasValue && eligibleQuantity < code.MinQuantity.Value)
            return DiscountApplication.Invalid(DiscountValidationStatus.MinQuantityNotMet, subtotal, $"This code requires at least {code.MinQuantity.Value} eligible ticket(s)");

        var eligibleSubtotal = eligibleLines.Sum(l => l.UnitPrice * l.Quantity);

        decimal discount = code.DiscountType switch
        {
            DiscountType.Percentage => Math.Round(eligibleSubtotal * (code.Value / 100m), 2, MidpointRounding.AwayFromZero),
            DiscountType.FixedAmount => code.Value,
            _ => 0m
        };

        discount = Math.Min(discount, eligibleSubtotal);
        if (subtotal - discount < MinimumPayableTotal)
            discount = subtotal - MinimumPayableTotal;

        if (discount < 0m) discount = 0m;

        return new DiscountApplication
        {
            Status = DiscountValidationStatus.Valid,
            Code = code,
            Subtotal = subtotal,
            DiscountAmount = discount
        };
    }

    public async Task<DiscountApplication> Consume(string storeId, string eventId, string rawCode, IReadOnlyList<DiscountCartLine> cart)
    {
        var subtotal = cart.Sum(l => l.UnitPrice * l.Quantity);
        var normalized = Normalize(rawCode);
        if (string.IsNullOrEmpty(normalized))
            return DiscountApplication.Invalid(DiscountValidationStatus.NotFound, subtotal, "Enter a discount code");

        await using var ctx = dbContextFactory.CreateContext();
        await using var tx = await ctx.Database.BeginTransactionAsync();

        var code = await ctx.DiscountCodes
            .FirstOrDefaultAsync(d => d.StoreId == storeId && d.EventId == eventId && d.Code == normalized);

        var application = Evaluate(code, cart, subtotal);
        if (!application.IsValid)
            return application;

        if (code.MaxUses.HasValue && code.UsesCount >= code.MaxUses.Value)
            return DiscountApplication.Invalid(DiscountValidationStatus.MaxUsesReached, subtotal, "That discount code has just reached its usage limit.");

        code.UsesCount += 1;
        await ctx.SaveChangesAsync();
        await tx.CommitAsync();
        return application;
    }
}
