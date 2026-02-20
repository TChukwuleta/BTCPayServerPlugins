using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.IdentityModel.Tokens;

namespace BTCPayServer.Plugins.Saleor.Services;

public class SaleorWebhookVerifier
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly Dictionary<string, (JsonWebKeySet Jwks, DateTime FetchedAt)> _jwksCache = new();
    private readonly SemaphoreSlim _cacheLock = new(1, 1);
    private static readonly TimeSpan JwksCacheDuration = TimeSpan.FromHours(1);

    public SaleorWebhookVerifier(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<bool> Verify(byte[] rawBody, string jwsSignature, string saleorApiUrl)
    {
        if (string.IsNullOrEmpty(jwsSignature)) return false;
        try
        {
            var parts = jwsSignature.Split('.');
            if (parts.Length != 3 || parts[1] != "") return false;

            var headerJson = Encoding.UTF8.GetString(Base64UrlDecode(parts[0]));
            using var headerDoc = JsonDocument.Parse(headerJson);
            var kid = headerDoc.RootElement.GetProperty("kid").GetString();
            var alg = headerDoc.RootElement.GetProperty("alg").GetString();
            if (alg != "RS256") return false;

            var jwks = await GetJwksAsync(saleorApiUrl);
            var key = jwks.Keys.FirstOrDefault(k => k.Kid == kid);
            if (key is null)
            {
                jwks = await GetJwksAsync(saleorApiUrl, forceRefresh: true);
                key = jwks.Keys.FirstOrDefault(k => k.Kid == kid);
                if (key is null) return false;
            }
            var b64 = headerDoc.RootElement.TryGetProperty("b64", out var b64Prop) && b64Prop.GetBoolean();
            byte[] signingInput;
            if (b64)
            {
                var bodyEncoded = Base64UrlEncode(rawBody);
                signingInput = Encoding.ASCII.GetBytes($"{parts[0]}.{bodyEncoded}");
            }
            else
            {
                var headerPrefix = Encoding.ASCII.GetBytes($"{parts[0]}.");
                signingInput = headerPrefix.Concat(rawBody).ToArray();
            }
            var signatureBytes = Base64UrlDecode(parts[2]);

            using var rsa = RSA.Create();
            rsa.ImportParameters((key.Kty == "RSA")
                ? new RSAParameters
                {
                    Modulus = Base64UrlDecode(key.N),
                    Exponent = Base64UrlDecode(key.E)
                }
                : throw new Exception("Expected RSA key"));

            return rsa.VerifyData(signingInput, signatureBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        }
        catch (Exception) { return false; }
    }

    private async Task<JsonWebKeySet> GetJwksAsync(string saleorApiUrl, bool forceRefresh = false)
    {
        var baseUrl = new Uri(saleorApiUrl).GetLeftPart(UriPartial.Authority);
        var jwksUrl = $"{baseUrl}/.well-known/jwks.json";
        await _cacheLock.WaitAsync();
        try
        {
            if (!forceRefresh && _jwksCache.TryGetValue(jwksUrl, out var cached) && DateTime.UtcNow - cached.FetchedAt < JwksCacheDuration)
            {
                return cached.Jwks;
            }
            var client = _httpClientFactory.CreateClient();
            var json = await client.GetStringAsync(jwksUrl);
            var jwks = new JsonWebKeySet(json);
            _jwksCache[jwksUrl] = (jwks, DateTime.UtcNow);
            return jwks;
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    public async Task<byte[]> ReadRawBodyAsync(HttpRequest request)
    {
        request.EnableBuffering();
        using var ms = new MemoryStream();
        await request.Body.CopyToAsync(ms);
        request.Body.Position = 0;
        return ms.ToArray();
    }

    private static byte[] Base64UrlDecode(string input)
    {
        var padded = input.Replace('-', '+').Replace('_', '/');
        switch (padded.Length % 4)
        {
            case 2: padded += "=="; break;
            case 3: padded += "="; break;
        }
        return Convert.FromBase64String(padded);
    }

    private static string Base64UrlEncode(byte[] input)
    {
        return Convert.ToBase64String(input).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
