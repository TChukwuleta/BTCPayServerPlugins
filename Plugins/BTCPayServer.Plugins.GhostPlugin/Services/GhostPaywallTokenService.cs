using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using NBitcoin;
using NBitcoin.DataEncoders;

namespace BTCPayServer.Plugins.GhostPlugin.Services;

public class GhostPaywallTokenService(GhostDbContextFactory dbContextFactory)
{
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromDays(400);

    public async Task<string> EnsurePaywallSecret(string storeId)
    {
        await using var ctx = dbContextFactory.CreateContext();
        var setting = ctx.GhostSettings.FirstOrDefault(c => c.StoreId == storeId);
        if (setting is null) return null;

        if (string.IsNullOrEmpty(setting.PaywallSecret))
        {
            setting.PaywallSecret = Encoders.Base58.EncodeData(RandomUtils.GetBytes(32));
            ctx.GhostSettings.Update(setting);
            await ctx.SaveChangesAsync();
        }
        return setting.PaywallSecret;
    }

    public string IssueUnlockToken(string secret, string contentId)
    {
        var expiresAtUnixSeconds = DateTimeOffset.UtcNow.Add(TokenLifetime).ToUnixTimeSeconds();
        var payload = $"{contentId}|{expiresAtUnixSeconds}";
        var payloadB64 = Base64UrlEncode(Encoding.UTF8.GetBytes(payload));
        var signatureB64 = Base64UrlEncode(Sign(secret, payloadB64));
        return $"{payloadB64}.{signatureB64}";
    }

    public bool VerifyUnlockToken(string secret, string contentId, string token)
    {
        if (string.IsNullOrEmpty(secret) || string.IsNullOrEmpty(contentId) || string.IsNullOrEmpty(token))
            return false;

        var parts = token.Split(".");
        if(parts.Length != 2) return false;

        var expectedSignature = Sign(secret, parts[0]);
        byte[] providedSignature;
        string payload;
        try
        {
            providedSignature = Base64UrlDecode(parts[1]);
            if(providedSignature.Length != expectedSignature.Length || !CryptographicOperations.FixedTimeEquals(providedSignature, expectedSignature)) 
                return false;

            payload = Encoding.UTF8.GetString(Base64UrlDecode(parts[0]));
            var payloadParts = payload.Split("|");
            if (payloadParts.Length != 2) return false;

            var tokenContentId = payloadParts[0];
            if (!string.Equals(tokenContentId, contentId, StringComparison.Ordinal))
                return false;

            if (!long.TryParse(payloadParts[1], out var expiresAtUnixSeconds))
                return false;

            return DateTimeOffset.UtcNow.ToUnixTimeSeconds() <= expiresAtUnixSeconds;
        }
        catch { return false; }
    }

    private static byte[] Sign(string secret, string payloadB64)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        return hmac.ComputeHash(Encoding.UTF8.GetBytes(payloadB64));
    }

    private static string Base64UrlEncode(byte[] data) => Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] Base64UrlDecode(string data)
    {
        var padded = data.Replace('-', '+').Replace('_', '/');
        switch (padded.Length % 4)
        {
            case 2: padded += "=="; break;
            case 3: padded += "="; break;
        }
        return Convert.FromBase64String(padded);
    }
}
