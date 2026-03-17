using System;
using System.ComponentModel.DataAnnotations;

namespace BTCPayServer.Plugins.ServerAlert.Data;

public class UserEmailPreference
{
    [Key]
    public string Id { get; set; } // BTCPayServer UserId
    public bool EmailEnabled { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}