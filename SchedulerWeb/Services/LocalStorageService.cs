using System.Threading.Tasks;
using Microsoft.JSInterop;

namespace SchedulerWeb.Services;

public sealed class LocalStorageService
{
    private readonly IJSRuntime _js;

    public LocalStorageService(IJSRuntime js)
    {
        _js = js;
    }

    public ValueTask SetStringAsync(string key, string value)
    {
        return _js.InvokeVoidAsync("localStorage.setItem", key, value);
    }

    public ValueTask<string?> GetStringAsync(string key)
    {
        return _js.InvokeAsync<string?>("localStorage.getItem", key);
    }

    public ValueTask RemoveAsync(string key)
    {
        return _js.InvokeVoidAsync("localStorage.removeItem", key);
    }
}

