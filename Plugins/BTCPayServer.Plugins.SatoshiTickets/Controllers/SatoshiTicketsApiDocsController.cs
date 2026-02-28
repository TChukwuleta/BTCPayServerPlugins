using System.Reflection;
using Microsoft.AspNetCore.Mvc;

namespace BTCPayServer.Plugins.SatoshiTickets.Controllers;

[Route("~/plugins/satoshi-tickets/api-docs")]
public class SatoshiTicketsApiDocsController : Controller
{
    private static readonly Assembly Assembly = typeof(SatoshiTicketsApiDocsController).Assembly;
    private const string ResourcePrefix = "BTCPayServer.Plugins.SatoshiTickets.Resources.";

    [HttpGet]
    public IActionResult Index()
    {
        var specUrl = Url.Action(nameof(Swagger));
        var redocUrl = Url.Action(nameof(RedocScript));
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
    <script src=""{redocUrl}""></script>
</body>
</html>";
        return Content(html, "text/html");
    }

    [HttpGet("swagger.json")]
    public IActionResult Swagger()
    {
        var stream = Assembly.GetManifestResourceStream(ResourcePrefix + "swagger.json");
        if (stream == null)
            return NotFound();
        return File(stream, "application/json");
    }

    [HttpGet("redoc.js")]
    [ResponseCache(Duration = 86400)]
    public IActionResult RedocScript()
    {
        var stream = Assembly.GetManifestResourceStream(ResourcePrefix + "redoc.standalone.js");
        if (stream == null)
            return NotFound();
        return File(stream, "application/javascript");
    }
}
