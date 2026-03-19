using System.Reflection;
using System.Text.Json;

/*var excluded = new HashSet<string> { "BTCPayServer.Plugins.ShopifyPlugin", "BTCPayServer.Plugins.Salesforce", "BTCPayServer.Plugins.SquareUp" };
var plugins = Directory.GetDirectories("../../../../Plugins").Where(p => !excluded.Contains(Path.GetFileName(p)));*/

var included = new HashSet<string> { "BTCPayServer.Plugins.SatoshiTickets", "BTCPayServer.Plugins.JumpSeller", "BTCPayServer.Plugins.LightSpeed", "BTCPayServer.Plugins.ServerAlert" };
var plugins = Directory.GetDirectories("../../../../Plugins").Where(p => included.Contains(Path.GetFileName(p)));
var p = "";
foreach (var plugin in plugins)
{
    var assemblyConfigurationAttribute = typeof(Program).Assembly.GetCustomAttribute<AssemblyConfigurationAttribute>();
    var buildConfigurationName = assemblyConfigurationAttribute?.Configuration;

    p += $"{Path.GetFullPath(plugin)}/bin/{buildConfigurationName}/net8.0/{Path.GetFileName(plugin)}.dll;";
}

var content = JsonSerializer.Serialize(new
{
    DEBUG_PLUGINS = p
});

Console.WriteLine(content);
await File.WriteAllTextAsync("../../../../btcpayserver/BTCPayServer/appsettings.dev.json", content);