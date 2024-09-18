using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace BTCPayServer.Plugins.ShopifyPlugin.Services;

public class CorsMiddleware
{
    private readonly RequestDelegate _next;

    public CorsMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
        context.Response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, PUT, DELETE, OPTIONS");
        context.Response.Headers.Add("Access-Control-Allow-Headers", "Content-Type, Authorization");

        if (context.Request.Method == "OPTIONS")
        {
            context.Response.StatusCode = 204; // No Content
            return;
        }

        await _next(context);
    }
}