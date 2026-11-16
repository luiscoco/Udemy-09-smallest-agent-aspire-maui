using System.Net;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SmallestAgent.Api.Services;
using SmallestAgent.Shared.Services;
using SmallestAgent.Shared.Components;
using SmallestAgent.Shared.Components.Pages;
using Microsoft.JSInterop;

var passed = 0;
async Task Check(string name, Func<Task> test) { await test(); Console.WriteLine($"PASS {name}"); passed++; }
void Assert(bool condition, string message = "Assertion failed") { if (!condition) throw new Exception(message); }
var secret = "sk-ant-unit-test-secret";
var settings = new AgentSettings(secret, "claude-sonnet-5-5", 1024, "Be concise.");
var runner = new FakeRunner();
var builder = WebApplication.CreateBuilder(new WebApplicationOptions { ContentRootPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../src/SmallestAgent.Api")) });
builder.WebHost.UseTestServer();
builder.Logging.ClearProviders();
builder.Services.AddSingleton(settings);
builder.Services.AddSingleton<IAgentRunner>(runner);
builder.Services.AddRazorComponents();
builder.Services.AddScoped<NavigationManager, CheckNavigationManager>();
builder.Services.AddSingleton<LessonCatalog>();
builder.Services.AddScoped<IHostActions, BrowserHostActions>();
builder.Services.AddSingleton<IAgentApiClient, FakeApiClient>();
builder.Services.AddSingleton(new HostInfo("Component test", "http://localhost:8400/docs"));
await using var app = builder.Build();
app.MapAgentEndpoints();
await app.StartAsync();
using var http = app.GetTestClient();

await Check("health returns presence without credentials", async () => {
    var body = await http.GetStringAsync("/api/health");
    Assert(body.Contains("\"api_key_configured\":true") && !body.Contains(secret));
});
await Check("query trims input and returns snake_case result", async () => {
    var response = await http.PostAsJsonAsync("/api/agent/query", new { prompt = " Hello " });
    var data = await response.Content.ReadFromJsonAsync<JsonElement>();
    Assert(response.IsSuccessStatusCode && data.GetProperty("result").GetString() == "Hello!");
    Assert(data.GetProperty("num_turns").GetInt32() == 1 && runner.LastPrompt == "Hello");
});
foreach (var route in new[] { "query", "stream" })
{
    await Check($"{route} rejects invalid prompts without SDK work", async () => {
        var calls = runner.Calls;
        foreach (var prompt in new string?[] { null, "", "   ", new('x', 4001) })
            Assert((int)(await http.PostAsJsonAsync($"/api/agent/{route}", new { prompt })).StatusCode == 422);
        Assert(runner.Calls == calls);
    });
    await Check($"{route} validates JSON, content type and body size", async () => {
        Assert((int)(await http.PostAsync($"/api/agent/{route}", new StringContent("{broken", Encoding.UTF8, "application/json"))).StatusCode == 400);
        Assert((int)(await http.PostAsync($"/api/agent/{route}", new StringContent("{}"))).StatusCode == 415);
        Assert((int)(await http.PostAsJsonAsync($"/api/agent/{route}", new { prompt = new string('x', 40000) })).StatusCode == 413);
    });
    await Check($"{route} accepts matching localhost/loopback and rejects foreign origin", async () => {
        foreach (var host in new[] { "localhost:8400", "127.0.0.1:8400" })
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/agent/{route}") { Content = JsonContent.Create(new { prompt = "" }) };
            request.Headers.Host = host; request.Headers.Add("Origin", $"http://{host}");
            Assert((int)(await http.SendAsync(request)).StatusCode == 422);
        }
        foreach (var origin in new[] { "https://foreign.invalid", "http://127.0.0.1:8401", "null" })
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/agent/{route}") { Content = JsonContent.Create(new { prompt = "Hi" }) };
            request.Headers.Host = "127.0.0.1:8400"; request.Headers.Add("Origin", origin);
            request.Headers.Add("X-Forwarded-Host", "foreign.invalid");
            Assert((int)(await http.SendAsync(request)).StatusCode == 403);
        }
    });
}
await Check("SSE forwards messages then done", async () => {
    var response = await http.PostAsJsonAsync("/api/agent/stream", new { prompt = "Hello" });
    var body = await response.Content.ReadAsStringAsync();
    Assert(response.Content.Headers.ContentType?.MediaType == "text/event-stream");
    Assert(body.Contains("SystemMessage") && body.Contains("ResultMessage") && body.Contains("event: done"));
    Assert(body.IndexOf("ResultMessage", StringComparison.Ordinal) < body.IndexOf("event: done", StringComparison.Ordinal));
});
await Check("provider failures are safe and SSE has no done", async () => {
    runner.Fail = true;
    var query = await http.PostAsJsonAsync("/api/agent/query", new { prompt = "Hello" });
    Assert((int)query.StatusCode == 502 && !(await query.Content.ReadAsStringAsync()).Contains("private-secret"));
    var body = await (await http.PostAsJsonAsync("/api/agent/stream", new { prompt = "Hello" })).Content.ReadAsStringAsync();
    Assert(body.Contains("event: error") && !body.Contains("event: done") && !body.Contains("private-secret"));
    runner.Fail = false;
});
await Check("missing final result becomes SSE error", async () => {
    runner.Empty = true;
    var body = await (await http.PostAsJsonAsync("/api/agent/stream", new { prompt = "Hello" })).Content.ReadAsStringAsync();
    Assert(body.Contains("event: error") && !body.Contains("event: done"));
    runner.Empty = false;
});
await Check("missing key skips SDK and gives setup instructions", async () => {
    var missingBuilder = WebApplication.CreateBuilder(); missingBuilder.WebHost.UseTestServer(); missingBuilder.Logging.ClearProviders();
    missingBuilder.Services.AddSingleton(settings with { ApiKey = "" }); missingBuilder.Services.AddSingleton<IAgentRunner>(runner);
    await using var missing = missingBuilder.Build(); missing.MapAgentEndpoints(); await missing.StartAsync();
    using var client = missing.GetTestClient(); var calls = runner.Calls;
    Assert((int)(await client.PostAsJsonAsync("/api/agent/query", new { prompt = "Hello" })).StatusCode == 503);
    var body = await (await client.PostAsJsonAsync("/api/agent/stream", new { prompt = "Hello" })).Content.ReadAsStringAsync();
    Assert(body.Contains("event: error") && body.Contains(".env") && runner.Calls == calls);
});
await Check("real C# SDK parses mocked provider stream and projects only public fields", async () => {
    var transport = new StubTransport(async (request, token) => {
        var input = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(token)).RootElement;
        Assert(input.GetProperty("tools").GetArrayLength() == 0 && input.GetProperty("max_tokens").GetInt32() == 1024);
        Assert(request.Headers.Any(header => header.Value.Any(value => value.Contains(secret, StringComparison.Ordinal))), "SDK must authenticate with the configured test key");
        return StreamResponse(ProviderStream("end_turn", $"Hello {secret}"));
    });
    var service = new ClaudeAgentRunner(settings, new ClientFactory(transport));
    var messages = new List<AgentMessage>();
    await foreach (var message in service.StreamAsync("Hello", default)) messages.Add(message);
    var result = (AgentResult)messages.Last().Data;
    Assert(result.Result == "Hello [redacted]" && !result.IsError && result.InputTokens == 7 && result.OutputTokens == 3);
    Assert(result.TotalCostUsd is null && !JsonSerializer.Serialize(messages).Contains(secret));
});
await Check("real SDK result marks max_tokens as early stop", async () => {
    var service = new ClaudeAgentRunner(settings, new ClientFactory(new StubTransport((_, _) => Task.FromResult(StreamResponse(ProviderStream("max_tokens", "Partial"))))));
    Assert((await service.QueryAsync("Hello", default)).IsError);
});
await Check("key redaction spans provider text chunk boundaries", async () => {
    var first = JsonSerializer.Serialize(new { type="content_block_delta", index=0, delta=new { type="text_delta", text=secret[..9] } });
    var second = JsonSerializer.Serialize(new { type="content_block_delta", index=0, delta=new { type="text_delta", text=secret[9..] } });
    var whole = JsonSerializer.Serialize(new { type="content_block_delta", index=0, delta=new { type="text_delta", text=secret } });
    var stream = ProviderStream("end_turn",secret).Replace(whole,$"{first}\n\nevent: content_block_delta\ndata: {second}");
    var service = new ClaudeAgentRunner(settings,new ClientFactory(new StubTransport((_,_)=>Task.FromResult(StreamResponse(stream)))));
    var chunks = new StringBuilder();
    await foreach (var message in service.StreamAsync("Hello",default))
        if (message.Type == "TextDelta") chunks.Append(JsonSerializer.SerializeToElement(message.Data).GetProperty("text").GetString());
    Assert(chunks.ToString() == "[redacted]");
});
await Check("real SDK rejects truncated provider stream", async () => {
    var service = new ClaudeAgentRunner(settings, new ClientFactory(new StubTransport((_, _) => Task.FromResult(StreamResponse(ProviderStream("end_turn", "Hi").Replace("event: message_stop\ndata: {\"type\":\"message_stop\"}\n\n", ""))))));
    try { await service.QueryAsync("Hello", default); throw new Exception("Expected failure"); } catch (InvalidOperationException) { }
});
await Check("cancellation reaches the real SDK HTTP boundary", async () => {
    var observed = false;
    var service = new ClaudeAgentRunner(settings, new ClientFactory(new StubTransport(async (_, token) => {
        try { await Task.Delay(Timeout.Infinite, token); } catch (OperationCanceledException) { observed = true; throw; }
        return StreamResponse("");
    })));
    using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
    try { await service.QueryAsync("Hello", cancellation.Token); throw new Exception("Expected cancellation"); } catch (OperationCanceledException) { }
    Assert(observed);
});
await Check("code highlighting encodes HTML before inserting colored spans", () => {
    var code = Presentation.Highlight("var x = \"<script>alert(1)</script>\"; // comment");
    Assert(code.Contains("hljs-keyword") && code.Contains("hljs-string") && code.Contains("hljs-comment") && !code.Contains("<script>"));
    return Task.CompletedTask;
});
await Check("Markdown strips active markup and highlights fenced code", () => {
    var html = Presentation.MarkdownHtml("[bad](javascript:alert%281%29)\n\n<img src=x onerror=alert(1)>\n\n```csharp\nvar x = 42;\n```");
    Assert(!html.Contains("href=\"javascript:") && !html.Contains("<img") && !html.Contains("<script") && html.Contains("hljs-keyword"));
    return Task.CompletedTask;
});
await Check("configuration recognizes templates and rejects invalid limits", () => {
    Assert(!(settings with { ApiKey = "sk-ant-replace-with-your-key" }).Configured);
    try { AgentSettings.Load(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> { ["AGENT_MAX_TOKENS"] = "0" }).Build(), Path.GetTempPath()); throw new Exception("Expected failure"); } catch (InvalidOperationException) { }
    return Task.CompletedTask;
});
await Check("syntax-highlighted Razor component renders without browser interop", async () => {
    await using var scope = app.Services.CreateAsyncScope();
    await using var renderer = new HtmlRenderer(scope.ServiceProvider, scope.ServiceProvider.GetRequiredService<ILoggerFactory>());
    var html = await renderer.Dispatcher.InvokeAsync(async () => (await renderer.RenderComponentAsync<CodeBlock>(ParameterView.FromDictionary(new Dictionary<string,object?> { ["Code"]="var answer = 42;" }))).ToHtmlString());
    Assert(html.Contains("hljs-keyword") && html.Contains("Copy code"));
});
await Check("all lesson tabs rerender the SAME component when the URL changes", async () => {
    await using var scope = app.Services.CreateAsyncScope();
    var navigation = scope.ServiceProvider.GetRequiredService<NavigationManager>();
    await using var renderer = new HtmlRenderer(scope.ServiceProvider, scope.ServiceProvider.GetRequiredService<ILoggerFactory>());
    var view = await renderer.Dispatcher.InvokeAsync(async () => await renderer.RenderComponentAsync<AgentLab>());
    foreach (var (path, heading) in new[] {
        ("/learn", "Small enough to understand."),
        ("/setup", "One AppHost. Shared native and web UI."),
        ("/architecture", "See how the pieces connect."),
        ("/walkthrough", "The same lesson. Native C#."),
        ("/", "Start small. See everything.") })
    {
        await renderer.Dispatcher.InvokeAsync(() => navigation.NavigateTo(path));
        var html = await renderer.Dispatcher.InvokeAsync(view.ToHtmlString);
        Assert(html.Contains(heading), $"Tab {path} did not update the existing component");
        Assert(html.Contains($"href=\"{path}\" aria-current=\"page\""), $"Tab {path} is not active");
        if (path == "/architecture") Assert(html.Split("class=\"lesson-step workflow-code-step\"").Length - 1 == 16);
    }
});
await SharedChecks.Run(Check, Assert);
Console.WriteLine($"All {passed} checks passed. No real API requests or credentials used.");

static HttpResponseMessage StreamResponse(string text) => new(HttpStatusCode.OK) { Content = new StringContent(text, Encoding.UTF8, "text/event-stream") };
static string ProviderStream(string reason, string text)
{
    var events = new object[] {
        new { type="message_start", message=new { id="msg_test", type="message", role="assistant", model="claude-sonnet-5-5", content=Array.Empty<object>(), stop_reason=(string?)null, stop_sequence=(string?)null, usage=new { input_tokens=7, output_tokens=0 } } },
        new { type="content_block_start", index=0, content_block=new { type="text", text="" } },
        new { type="content_block_delta", index=0, delta=new { type="text_delta", text } },
        new { type="content_block_stop", index=0 },
        new { type="message_delta", delta=new { stop_reason=reason, stop_sequence=(string?)null }, usage=new { output_tokens=3 } },
        new { type="message_stop" }
    };
    return string.Concat(events.Select(value => { var json = JsonSerializer.Serialize(value); return $"event: {JsonDocument.Parse(json).RootElement.GetProperty("type").GetString()}\ndata: {json}\n\n"; }));
}
sealed class ClientFactory(HttpMessageHandler handler) : IHttpClientFactory { public HttpClient CreateClient(string name) => new(handler, disposeHandler: false); }
sealed class CheckNavigationManager : NavigationManager
{
    public CheckNavigationManager() => Initialize("http://localhost:8400/", "http://localhost:8400/");
    protected override void NavigateToCore(string uri, bool forceLoad) { Uri = ToAbsoluteUri(uri).AbsoluteUri; NotifyLocationChanged(false); }
}
sealed class StubTransport(Func<HttpRequestMessage,CancellationToken,Task<HttpResponseMessage>> send) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => send(request, cancellationToken);
}
sealed class FakeRunner : IAgentRunner
{
    public int Calls; public string? LastPrompt; public bool Fail, Empty;
    public async Task<AgentResult> QueryAsync(string prompt, CancellationToken cancellationToken) { AgentResult? result = null; await foreach (var message in StreamAsync(prompt,cancellationToken)) if (message.Data is AgentResult final) result=final; return result ?? throw new InvalidOperationException(); }
    public async IAsyncEnumerable<AgentMessage> StreamAsync(string prompt, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        Calls++; LastPrompt=prompt; await Task.Yield(); cancellationToken.ThrowIfCancellationRequested();
        if (Fail) throw new Exception("private-secret /private/path");
        yield return new("SystemMessage","Ready",new { model="test" });
        if (!Empty) yield return new("ResultMessage","Complete",new AgentResult("Hello!",1,10,7,3,"end_turn",false));
    }
}
