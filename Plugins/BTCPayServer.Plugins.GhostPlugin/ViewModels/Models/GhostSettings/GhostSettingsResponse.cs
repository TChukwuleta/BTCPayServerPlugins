using System.Collections.Generic;

namespace BTCPayServer.Plugins.GhostPlugin.ViewModels.Models;

public class GhostSettingsResponse
{
    public List<GhostSettingResponseVm> settings { get; set; }
    public object meta { get; set; }
}


public class GhostSettingResponseVm
{
    public string key { get; set; }
    public object value { get; set; }
}