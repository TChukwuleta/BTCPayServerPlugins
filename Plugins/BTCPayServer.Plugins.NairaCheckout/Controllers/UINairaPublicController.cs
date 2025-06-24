using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System.IO;
using System.Linq;
using System.Text;
using System;
using BTCPayServer.Plugins.NairaCheckout.Services;
using System.Security.Cryptography;
using BTCPayServer.Plugins.NairaCheckout.ViewModels;

namespace BTCPayServer.Plugins.Template;

[AllowAnonymous]
[Route("~/plugins/{storeId}/naira-checkout/public/", Order = 0)]
[Route("~/plugins/{storeId}/naira-checkout/api/", Order = 1)]
public class UINairaPublicController : Controller
{
    private readonly NairaCheckoutDbContextFactory _dbContextFactory;
    public UINairaPublicController(NairaCheckoutDbContextFactory dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }


    [HttpPost("mavapay/webhook")]
    public async Task<IActionResult> ReceiveMavapayWebhook(string storeId)
    {
        try
        {
            using var reader = new StreamReader(Request.Body, Encoding.UTF8);
            var requestBody = await reader.ReadToEndAsync();
            if (string.IsNullOrEmpty(requestBody))
                return BadRequest("Empty request body");

            var basePayload = JsonConvert.DeserializeObject<MavapayWebhookResponseVm>(requestBody);
            if (basePayload?.@event == "ping")
            {
                return Ok(new { message = "Ping event received. Webhook is active." });
            }

            await using var ctx = _dbContextFactory.CreateContext();
            var mavapaySetting = ctx.MavapaySettings.FirstOrDefault(m => m.StoreId == storeId);
            if (mavapaySetting == null)
                return BadRequest();

            if (!Request.Headers.TryGetValue("x-webhook-Signature", out var signatureHeaderValues) ||
                    string.IsNullOrEmpty(signatureHeaderValues.FirstOrDefault()))
            {
                return BadRequest("Missing signature header");
            }
            /*if (!ValidateSignature(requestBody, signatureHeaderValues.First(), mavapaySetting.WebhookSecret))
                return Unauthorized("Invalid webhook signature");*/

            var webhookResponse = JsonConvert.DeserializeObject<MavapayWebhookResponseVm>(requestBody);
            var order = ctx.NairaCheckoutOrders.FirstOrDefault(c => c.ExternalHash == webhookResponse.data.hash && c.StoreId == storeId);
            if (order == null)
                return BadRequest();

            switch (webhookResponse.@event)
            {
                case "payment.received":
                    order.ThirdPartyStatus = "PaymentReceived";
                    order.UpdatedAt = DateTime.UtcNow;
                    ctx.NairaCheckoutOrders.Update(order);
                    await ctx.SaveChangesAsync();
                    break;

                case "payment.sent":
                    order.ThirdPartyStatus = "PaymentSent";
                    order.ThirdPartyMarkedPaid = true;
                    order.UpdatedAt = DateTime.UtcNow;
                    ctx.NairaCheckoutOrders.Update(order);
                    await ctx.SaveChangesAsync();
                    break;

                default:
                    break;
            }
            return Ok(new { message = "Webhook processed successfully." });
        }
        catch (Exception)
        {
            return StatusCode(500, "Internal Server Error.");
        }
    }

    private bool ValidateSignature(string payload, string signature, string webhookSecret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(webhookSecret));
        var computedHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        var expectedSignature = BitConverter.ToString(computedHash).Replace("-", "").ToLower();
        return SecureCompare(signature, expectedSignature);
    }

    private bool SecureCompare(string a, string b)
    {
        if (a.Length != b.Length) return false;
        var result = 0;
        for (var i = 0; i < a.Length; i++)
        {
            result |= a[i] ^ b[i];
        }
        return result == 0;
    }
}
