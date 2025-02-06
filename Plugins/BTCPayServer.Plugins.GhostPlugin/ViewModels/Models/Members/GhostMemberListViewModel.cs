using System;
using System.Collections.Generic;
using BTCPayServer.Plugins.GhostPlugin.Data;

namespace BTCPayServer.Plugins.GhostPlugin.ViewModels.Models;

public class GhostMemberListViewModel
{
    public string Id { get; set; }
    public string MemberId { get; set; }
    public string Name { get; set; }
    public string Email { get; set; }
    public string TierId { get; set; }
    public string TierName { get; set; }
    public string StoreId { get; set; }
    public TierSubscriptionFrequency Frequency { get; set; }
    public DateTimeOffset PeriodEndDate { get; set; }
    public DateTimeOffset CreatedDate { get; set; }
    public List<GhostTransactionViewModel> Subscriptions { get; set; }
}

public class GhostTransactionViewModel
{
    public string StoreId { get; set; }
    public string InvoiceId { get; set; }
    public string InvoiceStatus { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; }
    public string MemberId { get; set; }
    public DateTimeOffset PeriodStartDate { get; set; }
    public DateTimeOffset PeriodEndDate { get; set; }
}

public class GhostMembersViewModel
{
    public List<GhostMemberListViewModel> Members { get; set; }
    public List<GhostMemberListViewModel> DisplayedMembers { get; set; }
    public bool Active { get; set; }
    public bool SoonToExpire { get; set; }
    public bool Expired { get; set; }
}
