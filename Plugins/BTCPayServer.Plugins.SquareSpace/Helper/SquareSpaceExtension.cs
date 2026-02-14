using BTCPayServer.Plugins.SquareSpace.Data;
using BTCPayServer.Plugins.SquareSpace.ViewModels;

namespace BTCPayServer.Plugins.SquareSpace.Helper;

public static class SquareSpaceExtension
{
    public static SquarespaceSettingsVm SquareSpaceSettingsToViewModel(this SquareSpaceSetting settings)
    {
        return new SquarespaceSettingsVm
        {
            OAuthToken = settings.OAuthToken,
            WebhookEndpointUrl = settings.WebhookEndpointUrl,
            WebhookSecret = settings.WebhookSecret,
            WebsiteId = settings.WebsiteId,
            WebhookSubscriptionId = settings.WebhookSubscriptionId,
            AutoCreateInvoices = settings.AutoCreateInvoices
        };
    }

    public static SquareSpaceSetting SquareSpaceViewModelToSettings(this SquarespaceSettingsVm settings)
    {
        return new SquareSpaceSetting
        {
            OAuthToken = settings.OAuthToken,
            WebhookEndpointUrl = settings.WebhookEndpointUrl,
            WebhookSecret = settings.WebhookSecret,
            WebsiteId = settings.WebsiteId,
            WebhookSubscriptionId = settings.WebhookSubscriptionId,
            AutoCreateInvoices = settings.AutoCreateInvoices
        };
    }
}
