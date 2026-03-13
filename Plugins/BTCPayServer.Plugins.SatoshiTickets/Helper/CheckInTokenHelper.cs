using System;
using System.Security.Cryptography;
using System.Text;
using BTCPayServer.Plugins.SatoshiTickets.Data;

namespace BTCPayServer.Plugins.SatoshiTickets.Helper;

public static class CheckInTokenHelper
{
    public static bool VerifyToken(string token, EventCheckInSettings settings)
    {
        if (string.IsNullOrEmpty(settings?.CheckInToken)) return false;
        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(settings.CheckInToken), Encoding.UTF8.GetBytes(token));
    }

    public static string HashPin(string pin)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(pin),
            salt,
            iterations: 100_000,
            HashAlgorithmName.SHA256,
            outputLength: 32);
        // Store salt + hash together
        return $"{Convert.ToHexString(salt)}:{Convert.ToHexString(hash)}";
    }

    public static bool VerifyPin(string pin, string storedHash)
    {
        var parts = storedHash.Split(':');
        if (parts.Length != 2) return false;
        var salt = Convert.FromHexString(parts[0]);
        var expectedHash = Convert.FromHexString(parts[1]);
        var actualHash = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(pin),
            salt,
            iterations: 100_000,
            HashAlgorithmName.SHA256,
            outputLength: 32);
        return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
    }

}