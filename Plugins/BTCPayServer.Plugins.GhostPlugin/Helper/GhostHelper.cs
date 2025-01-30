using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using BTCPayServer.Plugins.GhostPlugin.Data;
using BTCPayServer.Plugins.GhostPlugin.ViewModels;

namespace BTCPayServer.Plugins.GhostPlugin.Helper;

public class GhostHelper
{
    public async Task<(bool succeeded, string response)> GetCustomJavascript(string storeId, string baseUrl)
    {
        string[] fileUrls = new[]
        {
            "https://raw.githubusercontent.com/TChukwuleta/BTCPayServerPlugins/main/Plugins/BTCPayServer.Plugins.GhostPlugin/Resources/js/btcpay.js"
        };
        StringBuilder combinedJavascript = new StringBuilder();
        using (var httpClient = new HttpClient())
        {
            foreach (var fileUrl in fileUrls)
            {
                try
                {
                    string fileContent = await httpClient.GetStringAsync(fileUrl);
                    combinedJavascript.AppendLine(fileContent);
                }
                catch (HttpRequestException httpEx)
                {
                    return (false, $"Failed to fetch file content due to a network issue: {httpEx.Message}");
                }
                catch (Exception ex)
                {
                    return (false, $"Failed to fetch file content: {ex.Message}");
                }
            }
        }
        string jsVariables = $"var BTCPAYSERVER_URL = '{baseUrl}'; var STORE_ID = '{storeId}';";
        combinedJavascript.Insert(0, jsVariables + Environment.NewLine);
        return (true, combinedJavascript.ToString());
    }

    public GhostSettingViewModel GhostSettingsToViewModel(GhostSetting ghostSetting)
    {
        return new GhostSettingViewModel
        {
            AdminApiKey = ghostSetting.AdminApiKey,
            ApiUrl = ghostSetting.ApiUrl,
            ContentApiKey = ghostSetting.ContentApiKey,
            Username = ghostSetting.Username,
            Password = ghostSetting.Password,
            StoreId = ghostSetting.StoreId,
            StoreName = ghostSetting.StoreName,
            IntegratedAt = ghostSetting.IntegratedAt
        };
    }

    public GhostSetting GhostSettingsViewModelToEntity(GhostSettingViewModel vm)
    {
        return new GhostSetting
        {
            AdminApiKey = vm.AdminApiKey,
            ApiUrl = vm.ApiUrl,
            ContentApiKey = vm.ContentApiKey,
            Username = vm.Username,
            Password = vm.Password,
            StoreId = vm.StoreId,
            StoreName = vm.StoreName
        };
    }
}