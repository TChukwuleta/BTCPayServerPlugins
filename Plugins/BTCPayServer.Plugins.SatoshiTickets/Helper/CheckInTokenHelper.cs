using System;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.DataProtection;

namespace BTCPayServer.Plugins.SatoshiTickets.Helper;

public static class CheckInTokenHelper
{
    public static string GenerateToken(string eventId, string storeId, IDataProtectionProvider dataProtectionProvider)
    {
        var protector = dataProtectionProvider.CreateProtector("SatoshiTickets.CheckIn");
        var raw = $"{eventId}:{storeId}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(protector.Protect(raw)));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public static bool VerifyToken(string token, string eventId, string storeId, IDataProtectionProvider dataProtectionProvider)
    {
        var expected = GenerateToken(eventId, storeId, dataProtectionProvider);
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected),
            Encoding.UTF8.GetBytes(token));
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