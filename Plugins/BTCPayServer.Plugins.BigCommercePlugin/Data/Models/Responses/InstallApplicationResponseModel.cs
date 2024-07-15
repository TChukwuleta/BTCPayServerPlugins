using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BTCPayServer.Plugins.BigCommercePlugin.Data.Models;

public class InstallApplicationResponseModel
{
    public string access_token { get; set; }
    public string scope { get; set; }
    public BigCommerceUser user { get; set; }
    public string context { get; set; }
}

public class BigCommerceUser
{
    public int id { get; set; }
    public string email { get; set; }
}
