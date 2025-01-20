using System;

namespace BTCPayServer.Plugins.GhostPlugin
{
    public class GhostApiException : Exception
    {
        public GhostApiException(string message) : base(message)
        {
        }
    }
}
