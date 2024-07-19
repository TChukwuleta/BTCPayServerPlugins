using System.Reflection;
using System.Text.Json;

var plugins = Directory.GetDirectories("../../../../Plugins");
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