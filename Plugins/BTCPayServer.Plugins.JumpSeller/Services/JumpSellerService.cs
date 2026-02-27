using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using BTCPayServer.Client.Models;
using BTCPayServer.Plugins.JumpSeller.Data;
using BTCPayServer.Plugins.JumpSeller.ViewModels;
using BTCPayServer.Services.Invoices;

namespace BTCPayServer.Plugins.JumpSeller.Services;

public class JumpSellerService
{
    public const string HttpClientName = "JumpSellerCallback";

    private readonly JumpSellerDbContextFactory _dbFactory;
    private readonly IHttpClientFactory _httpClientFactory;

    public JumpSellerService(JumpSellerDbContextFactory dbFactory, IHttpClientFactory httpClientFactory)
    {
        _dbFactory = dbFactory;
        _httpClientFactory = httpClientFactory;
    }


    public async Task<JumpSellerStoreSetting?> GetSettings(string storeId)
    {
        await using var db = _dbFactory.CreateContext();
        return db.JumpSellerStoreSettings.FirstOrDefault(c => c.StoreId == storeId);
    }

    public async Task SaveSettings(string storeId, JumpSellerSettingsViewModel settings)
    {
        await using var db = _dbFactory.CreateContext();
        var row = db.JumpSellerStoreSettings.FirstOrDefault(c => c.StoreId == storeId);
        if (row is null)
        {
            row = new JumpSellerStoreSetting { StoreId = storeId };
            db.JumpSellerStoreSettings.Add(row);
        }
        row.EpgAccountId = settings.EpgAccountId;
        row.EpgSecret = settings.EpgSecret;
        await db.SaveChangesAsync();
    }
    public bool VerifySignature(IDictionary<string, string> fields, string secret)
    {
        if (!fields.TryGetValue("x_signature", out var receivedSig))
            return false;

        var computed = ComputeSignature(fields, secret);
        return string.Equals(computed, receivedSig, StringComparison.OrdinalIgnoreCase);
    }

    public string ComputeSignature(IDictionary<string, string> fields, string secret)
    {
        // Sort x_ keys alphabetically, exclude x_signature itself.
        var sorted = fields.Where(kv => kv.Key.StartsWith("x_") && kv.Key != "x_signature").OrderBy(kv => kv.Key, StringComparer.Ordinal);
        var sb = new StringBuilder();
        foreach (var (key, value) in sorted)
        {
            var escapedValue = (key == "x_message" || key == "x_description") ? value.Replace("\n", "\\n") : value;
            sb.Append(key).Append(escapedValue);
        }
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public async Task SaveInvoiceData(JumpSellerInvoice data)
    {
        await using var db = _dbFactory.CreateContext();
        db.JumpSellerInvoices.Add(data);
        await db.SaveChangesAsync();
    }

    public async Task<JumpSellerInvoice?> GetInvoiceData(string invoiceId)
    {
        await using var db = _dbFactory.CreateContext();
        return db.JumpSellerInvoices.Find(invoiceId);
    }
    public async Task<JumpSellerInvoice?> GetInvoiceDataByReference(string storeId, string reference)
    {
        await using var db = _dbFactory.CreateContext();
        return db.JumpSellerInvoices.FirstOrDefault(c => c.StoreId == storeId && c.OrderReference == reference);
    }

    public async Task SendCallback(JumpSellerInvoice invoiceData, JumpSellerStoreSetting settings, string result, string message = "")
    {
        // result =  "completed" | "pending" | "failed"
        await using var db = _dbFactory.CreateContext();
        var record = db.JumpSellerInvoices.Find(invoiceData.InvoiceId);
        if (record is null || (record.CallbackSent && record.LastResult == "completed"))
            return;

        var timestamp = DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
        var fields = new Dictionary<string, string>
        {
            ["x_account_id"] = settings.EpgAccountId,
            ["x_amount"] = invoiceData.Amount,
            ["x_currency"] = invoiceData.Currency,
            ["x_reference"] = invoiceData.OrderReference,
            ["x_result"] = result,
            ["x_timestamp"] = timestamp,
        };
        if (!string.IsNullOrEmpty(message))
            fields["x_message"] = message;

        fields["x_signature"] = ComputeSignature(fields, settings.EpgSecret);
        var client = _httpClientFactory.CreateClient(HttpClientName);
        var content = new FormUrlEncodedContent(fields);
        var maxAttempts = 3;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                var response = await client.PostAsync(invoiceData.CallbackUrl, content);
                if (response.IsSuccessStatusCode)
                {
                    record.CallbackSent = true;
                    record.LastResult = result;
                    await db.SaveChangesAsync();
                    return;
                }
            }
            catch (Exception){ }

            if (attempt < maxAttempts)
                await Task.Delay(TimeSpan.FromSeconds(attempt * 2));
        }
    }

    public string MapInvoiceStatusToResult(InvoiceEntity invoice, JumpSellerStoreSetting settings, out string message)
    {
        message = string.Empty;
        var status = invoice.Status;
        var exceptionStatus = invoice.ExceptionStatus;
        if (exceptionStatus == InvoiceExceptionStatus.PaidOver)
        {
            message = "Payment received. Note: overpayment detected — please review.";
            return "completed";
        }
        if (exceptionStatus == InvoiceExceptionStatus.PaidPartial)
        {
            message = "Partial payment received. Order requires manual review.";
            return "pending";
        }
        return status switch
        {
            // Allowing processing to stand as pending for now.. 
            InvoiceStatus.Settled => "completed",
            InvoiceStatus.Expired => "failed",
            InvoiceStatus.Invalid => "failed",
            _ => "pending"
        };
    }
}
