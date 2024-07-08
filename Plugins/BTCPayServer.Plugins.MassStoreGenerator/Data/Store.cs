using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace BTCPayServer.Plugins.MassStoreGenerator.Data;

public class Store
{
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public string Id { get; set; }
    public string StoreDataId { get; set; }
    public string StoreName { get; set; }
    public string StoreBlob { get; set; }
    public string ApplicationUserId { get; set; }
    public DateTimeOffset Timestamp { get; set; }
}
