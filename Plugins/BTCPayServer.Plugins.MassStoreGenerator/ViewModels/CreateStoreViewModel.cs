using BTCPayServer.Plugins.MassStoreGenerator.Helper;
using Microsoft.AspNetCore.Mvc.Rendering;
using Newtonsoft.Json.Linq;
using System.ComponentModel.DataAnnotations;

namespace BTCPayServer.Plugins.MassStoreGenerator.ViewModels
{
    public class CreateStoreViewModel
    {
        [Required]
        [MaxLength(50)]
        [MinLength(1)]
        [Display(Name = "Store Name")]
        public string Name { get; set; }

        [Required]
        [MaxLength(10)]
        [Display(Name = "Default Currency")]
        public string DefaultCurrency { get; set; }

        [Display(Name = "Price Source")]
        public bool HasStoreCreationPermission { get; set; }
        public string PreferredExchange { get; set; }

        public SelectList Exchanges { get; set; }
        public JObject RecommendedExchanges { get; set; } = StoreBlobHelper.RecommendedExchanges;
    }
}
