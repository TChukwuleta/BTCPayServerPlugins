using System;

namespace BTCPayServer.Plugins.SatoshiTickets
{
    public class SimpleTicketSalesApiException : Exception
    {
        public SimpleTicketSalesApiException(string message) : base(message)
        {
        }
    }
}
