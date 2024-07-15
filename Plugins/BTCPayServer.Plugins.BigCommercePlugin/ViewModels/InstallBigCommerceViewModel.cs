using System.ComponentModel.DataAnnotations;

namespace BTCPayServer.Plugins.BigCommercePlugin.ViewModels
{
    public class InstallBigCommerceViewModel
    {
        [Required]
        [Display(Name = "Client Id")]
        public string ClientId { get; set; }

        [Required]
        [Display(Name = "Client Secret")]
        public string ClientSecret { get; set; }
    }
}
