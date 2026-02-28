using System.IO;
using System.Reflection;
using Microsoft.AspNetCore.Mvc;

namespace BTCPayServer.Plugins.SatoshiTickets.Controllers;

[Route("~/plugins/satoshi-tickets/api-docs")]
public class SatoshiTicketsApiDocsController : Controller
{
    [HttpGet]
    public IActionResult Index()
    {
        var specUrl = Url.Action(nameof(Swagger));
        var html = $@"<!DOCTYPE html>
<html>
<head>
    <title>SatoshiTickets API Documentation</title>
    <meta charset=""utf-8"" />
    <meta name=""viewport"" content=""width=device-width, initial-scale=1"" />
    <style>body {{ margin: 0; padding: 0; }}</style>
</head>
<body>
    <redoc spec-url=""{specUrl}""></redoc>
    <script src=""https://cdn.redoc.ly/redoc/v2.4.0/bundles/redoc.standalone.js""></script>
</body>
</html>";
        return Content(html, "text/html");
    }

    [HttpGet("swagger.json")]
    public IActionResult Swagger()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var stream = assembly.GetManifestResourceStream("BTCPayServer.Plugins.SatoshiTickets.Resources.swagger.json");
        if (stream == null)
            return NotFound();
        return File(stream, "application/json");
    }
}
