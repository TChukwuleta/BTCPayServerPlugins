using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace BTCPayServer.Plugins.Saleor.Services;

public class SaleorWebhookVerifier
{
    private readonly ILogger<SaleorWebhookVerifier> _logger;

    public SaleorWebhookVerifier(ILogger<SaleorWebhookVerifier> logger)
    {
        _logger = logger;
    }

    public bool Verify(byte[] rawBody, string signatureHeader, string token)
    {
        if (string.IsNullOrEmpty(signatureHeader))
        {
            _logger.LogWarning("Webhook verification failed: missing saleor-signature header");
            return false;
        }

        try
        {
            var keyBytes = Encoding.UTF8.GetBytes(token);
            using var hmac = new HMACSHA256(keyBytes);
            var computedHash = hmac.ComputeHash(rawBody);
            var computedSignature = Convert.ToHexString(computedHash).ToLowerInvariant();

            var isValid = CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(computedSignature),
                Encoding.UTF8.GetBytes(signatureHeader.ToLowerInvariant())
            );

            if (!isValid)
            {
                _logger.LogWarning("Webhook signature mismatch. Expected: {Expected}, Got: {Got}",
                    computedSignature, signatureHeader);
            }

            return isValid;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying webhook signature");
            return false;
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
}
