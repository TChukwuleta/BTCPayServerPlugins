using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BTCPayServer.Plugins.GhostPlugin.ViewModels.Models;

public class CreateMemberResponseModel
{
    public List<MemberCreationResponse> members { get; set; } = new List<MemberCreationResponse>();
}
public class MemberCreationResponse
{
    public string id { get; set; }
    public string uuid { get; set; }
    public string unsubscribe_url { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JToken> AdditionalData { get; set; }
}