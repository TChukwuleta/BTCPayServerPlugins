using Microsoft.AspNetCore.Mvc.Rendering;
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

        [Display(Name = "Preferred Price Source")]
        public string PreferredExchange { get; set; }

        public SelectList Exchanges { get; set; }
    }
}
