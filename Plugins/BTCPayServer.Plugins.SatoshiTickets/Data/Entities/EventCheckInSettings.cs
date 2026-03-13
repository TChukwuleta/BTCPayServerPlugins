using System;
using System.Security.Cryptography;

namespace BTCPayServer.Plugins.SatoshiTickets.Data;

public class EventCheckInSettings
{
    public string EventId { get; set; }
    public string CheckInToken { get; set; } = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
    public string PinHash { get; set; }
    public bool PinEnabled { get; set; }
}
