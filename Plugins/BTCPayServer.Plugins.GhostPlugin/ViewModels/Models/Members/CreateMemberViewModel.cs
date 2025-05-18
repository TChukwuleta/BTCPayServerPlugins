using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using BTCPayServer.Plugins.GhostPlugin.Data;

namespace BTCPayServer.Plugins.GhostPlugin.ViewModels.Models;

public class CreateMemberViewModel : BaseGhostPublicViewModel
{
    public string ShopName { get; set; }
    public string Name { get; set; }

    [Required]
    [EmailAddress(ErrorMessage = "Invalid email address format.")]
    public string Email { get; set; }
    public List<Tier> GhostTiers { get; set; }
    public string TierId { get; set; }
    public TierSubscriptionFrequency TierSubscriptionFrequency { get; set; }
}
