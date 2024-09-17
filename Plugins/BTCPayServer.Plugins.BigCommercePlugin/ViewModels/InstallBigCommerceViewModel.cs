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

        [Display(Name = "Store Name")]
        public string StoreName { get; set; }

        [Display(Name = "Store Id")]
        public string StoreId { get; set; }

        [Display(Name = "Auth Callback URL")]
        public string AuthCallBackUrl { get; set; }

        [Display(Name = "Load Callback URL")]
        public string LoadCallbackUrl { get; set; }

        [Display(Name = "Uninstall Callback URL")]
        public string UninstallCallbackUrl { get; set; }

        [Display(Name = "Checkout Script URL")]
        public string CheckoutScriptUrl { get; set; }

        public string CryptoCode { get; set; }

        public bool HasStore { get; set; } = true;
    }
}
