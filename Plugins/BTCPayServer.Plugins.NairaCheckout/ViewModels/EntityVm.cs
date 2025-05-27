namespace BTCPayServer.Plugins.NairaCheckout.ViewModels;

public class EntityVm<T> where T : new()
{
    T obj;

    public EntityVm()
    {
        obj = new T();
    }

    public string status { get; set; }
    public T data { get; set; }
}