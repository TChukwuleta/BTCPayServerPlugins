using System;
using System.Security.Cryptography;
using System.Text;

namespace BTCPayServer.Plugins.SatoshiTickets.Helper;

public static class CheckInTokenHelper
{
    public static string GenerateToken(string eventId, string storeId)
    {
        var raw = $"{eventId}:{storeId}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public static bool VerifyToken(string token, string eventId, string storeId)
    {
        var expected = GenerateToken(eventId, storeId);
        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(token));
    }

    public static string HashPin(string pin)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(pin));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public static bool VerifyPin(string pin, string hash)
    {
        var expected = HashPin(pin);
        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(hash));
    }

}