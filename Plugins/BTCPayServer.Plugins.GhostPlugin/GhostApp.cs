using System;
using System.Threading.Tasks;
using BTCPayServer.Data;
using BTCPayServer.Plugins.GhostPlugin.Data;
using BTCPayServer.Services.Apps;

namespace BTCPayServer.Plugins.GhostPlugin
{
    public class GhostApp : AppBaseType
    {
        public const string AppType = "Ghost";
        public const string MemberIdKey = "memberId";
        public const string AppName = "GhostSubscription";
        public const string PaymentRequestSourceKey = "source";
        public const string PaymentRequestSubscriptionIdKey = "ghostsubscriptionId";
        public const string GHOST_PREFIX = "Ghost_";
        public const string GHOST_MEMBER_ID_PREFIX = "member-";
        public const string GHOST_TICKET_ID_PREFIX = "ticket-";
        public const string GhostSubscriptionRenewalRequested = "GhostSubscriptionRenewalRequested";

        public GhostApp()
        {
            Description = "Ghost";
            Type = AppType;
        }
        public override Task<string> ConfigureLink(AppData app)
        {
            throw new NotImplementedException();
        }

        public override Task<object> GetInfo(AppData appData)
        {
            throw new NotImplementedException();
        }
        public override Task SetDefaultSettings(AppData appData, string defaultCurrency)
        {
            appData.SetSettings(new GhostSetting());
            return Task.CompletedTask;
        }

        public override Task<string> ViewLink(AppData app)
        {
            throw new NotImplementedException();
        }
    }
}
