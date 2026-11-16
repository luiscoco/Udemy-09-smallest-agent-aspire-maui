using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SmallestAgent.Shared.Services;
namespace SmallestAgent.Maui;
public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.Configuration.AddEnvironmentVariables();
        builder.UseMauiApp<App>();
        builder.AddServiceDefaults();
        builder.Services.AddMauiBlazorWebView();
        builder.Services.AddSingleton<LessonCatalog>();
        builder.Services.AddSingleton<IHostActions, WindowsHostActions>();
        builder.Services.AddSingleton(new HostInfo("Windows MAUI Blazor Hybrid", "http://127.0.0.1:8400/docs"));
        // Aspire supplies service endpoints to this native process. Direct runs
        // may set AgentApi__BaseUrl; the loopback fallback is local development.
        var discovered = builder.Configuration.AsEnumerable().Any(item => item.Key.StartsWith("services:agent-api:", StringComparison.OrdinalIgnoreCase));
        var address = builder.Configuration["AgentApi:BaseUrl"] ?? (discovered ? "https+http://agent-api" : "http://127.0.0.1:8400");
        builder.Services.AddHttpClient<IAgentApiClient, AgentApiClient>(client => { client.BaseAddress = new Uri(address); client.Timeout = Timeout.InfiniteTimeSpan; });
#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif
        return builder.Build();
    }
}
