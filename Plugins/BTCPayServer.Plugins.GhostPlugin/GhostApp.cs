using System;
using System.Threading.Tasks;
using BTCPayServer.Configuration;
using BTCPayServer.Data;
using BTCPayServer.Services.Apps;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;

namespace BTCPayServer.Plugins.GhostPlugin
{
    public class GhostApp : AppBaseType
    {
        private readonly LinkGenerator _linkGenerator;
        private readonly IOptions<BTCPayServerOptions> _options;
        public const string AppType = "Ghost";
        public const string AppName = "GhostSubscription";
        public const string GHOST_MEMBER_ID_PREFIX = "Ghost_member-";

        public GhostApp(
            LinkGenerator linkGenerator,
            IOptions<BTCPayServerOptions> options)
        {
            Description = "Ghost";
            Type = AppType;
            _linkGenerator = linkGenerator;
            _options = options;
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
            throw new NotImplementedException();
        }

        public override Task<string> ViewLink(AppData app)
        {
            throw new NotImplementedException();
        }
    }
}
