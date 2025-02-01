using System;
using System.Collections.Generic;

namespace BTCPayServer.Plugins.GhostPlugin.ViewModels.Models;

public class CreateGhostMemberRequest
{
    public List<Member> members { get; set; }
}

public class Member
{
    public string email { get; set; }
    public string name { get; set; }
    public bool comped { get; set; }
    public List<MemberTier> tiers { get; set; }
}

public class MemberTier
{
    public DateTime expiry_at { get; set; }
    public string id { get; set; }
}