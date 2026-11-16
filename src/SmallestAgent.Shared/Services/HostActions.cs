using Microsoft.JSInterop;
namespace SmallestAgent.Shared.Services;
public interface IHostActions
{
    Task CopyAsync(string text);
    Task<string> SaveAsync(string filename, string mime, string text);
}
public sealed class BrowserHostActions(IJSRuntime js) : IHostActions
{
    public async Task CopyAsync(string text) => await js.InvokeVoidAsync("navigator.clipboard.writeText", text);
    public async Task<string> SaveAsync(string filename, string mime, string text)
    {
        await using var module = await js.InvokeAsync<IJSObjectReference>("import", "./_content/SmallestAgent.Shared/agent.js");
        await module.InvokeVoidAsync("download", text, filename, mime);
        return "Download started.";
    }
}
