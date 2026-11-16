using System.Net;
using System.Text;
using System.Text.Json;
using System.Runtime.CompilerServices;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using SmallestAgent.Shared.Services;
using SmallestAgent.Shared.Components.Pages;

static class SharedChecks
{
    public static async Task Run(Func<string, Func<Task>, Task> check, Action<bool,string> assert)
    {
        await check("typed SSE parser preserves UTF-8, CRLF and multiline data", async () => {
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(": heartbeat\r\n\r\nevent: message\r\ndata: {\"text\":\r\ndata: \"héllo\"}\r\n\r\nevent: done\ndata: {}\n\n"));
            var frames = new List<(string Event, JsonElement Data)>();
            await foreach (var frame in AgentApiClient.ReadEvents(stream)) frames.Add(frame);
            assert(frames.Count == 2 && frames[0].Data.GetProperty("text").GetString() == "héllo" && frames[1].Event == "done", "UTF-8 SSE framing");
        });
        await check("typed client requires result and done and posts JSON", async () => {
            var result = new AgentResult("Hi",1,42,7,3,"end_turn",false);
            var message = JsonSerializer.Serialize(new AgentMessage("ResultMessage","Complete",result),Contracts.Json);
            foreach (var text in new[] { $"event: message\ndata: {message}\n\nevent: done\ndata: {{}}\n\n", $"event: message\ndata: {message}\n\n", "event: done\ndata: {}\n\n" })
            {
                using var http = new HttpClient(new StubTransport(async (request,token) => { assert(request.Method == HttpMethod.Post && (await request.Content!.ReadAsStringAsync(token)).Contains("prompt"),"POST contract"); return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(text,Encoding.UTF8,"text/event-stream") }; })) { BaseAddress = new Uri("http://api") };
                var succeeded = false;
                try { await foreach (var item in new AgentApiClient(http).StreamAsync("Hi",default)) { } succeeded = true; } catch (AgentApiException) { }
                assert(succeeded == (text.Contains("ResultMessage") && text.Contains("event: done")),"Require final result and done");
            }
        });
        await check("typed client preserves safe HTTP and SSE error details", async () => {
            foreach (var streaming in new[] { false,true })
            {
                using var http = new HttpClient(new StubTransport((_,_) => Task.FromResult(new HttpResponseMessage(streaming ? HttpStatusCode.OK : HttpStatusCode.ServiceUnavailable) { Content = new StringContent(streaming ? "event: error\ndata: {\"detail\":\"Configure the key\"}\n\n" : "{\"detail\":\"Configure the key\"}") }))) { BaseAddress = new Uri("http://api") };
                var client = new AgentApiClient(http); var caught = false;
                try { if (streaming) { await foreach (var item in client.StreamAsync("Hi",default)) { } } else await client.QueryAsync("Hi",default); } catch (AgentApiException exception) { caught = exception.Message == "Configure the key"; }
                assert(caught,"Safe detail reaches UI");
            }
        });
        await check("interactive shared component runs and retains prompt across every tab", async () => {
            await using var context = new BunitContext();
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.JSInterop.SetupModule("./_content/SmallestAgent.Shared/agent.js");
            var fake = new FakeApiClient();
            context.Services.AddSingleton<IAgentApiClient>(fake);
            context.Services.AddSingleton<LessonCatalog>();
            context.Services.AddScoped<IHostActions, BrowserHostActions>();
            context.Services.AddSingleton(new HostInfo("Hybrid component test","http://localhost/docs"));
            var cut = context.Render<AgentLab>();
            cut.Find("#prompt").Input("  Remember this prompt  ");
            await cut.Find(".run-button").ClickAsync(new MouseEventArgs());
            cut.WaitForAssertion(() => assert(cut.Markup.Contains("data-state=\"success\"") && fake.LastPrompt == "Remember this prompt","Typed stream updates UI"));
            var navigation = context.Services.GetRequiredService<NavigationManager>();
            foreach (var path in new[] { "/learn","/setup","/architecture","/walkthrough","/" })
            {
                await cut.InvokeAsync(() => navigation.NavigateTo(path));
                cut.WaitForAssertion(() => assert(cut.Find("a[aria-current=page]").GetAttribute("href") == path,"Selected section"));
                if (path == "/architecture") assert(cut.FindAll(".workflow-code-step").Count == 16,"All 16 source cards");
            }
            assert(cut.Find("#prompt").GetAttribute("value") == "  Remember this prompt  " || cut.Find("#prompt").TextContent == "  Remember this prompt  ","Prompt retained");
        });
    }
}
sealed class FakeApiClient : IAgentApiClient
{
    public string? LastPrompt;
    public Task<HealthInfo> HealthAsync(CancellationToken token = default) => Task.FromResult(new HealthInfo("ok",true));
    public Task<AgentResult> QueryAsync(string prompt,CancellationToken token) { LastPrompt=prompt;return Task.FromResult(new AgentResult("**Hello!**",1,42,7,3,"end_turn",false)); }
    public async IAsyncEnumerable<ClientMessage> StreamAsync(string prompt,[EnumeratorCancellation]CancellationToken token)
    {
        LastPrompt=prompt;await Task.Yield();token.ThrowIfCancellationRequested();
        yield return new("SystemMessage","Ready",JsonSerializer.SerializeToElement(new { model="test" }));
        yield return new("ResultMessage","Complete",JsonSerializer.SerializeToElement(new AgentResult("**Hello!**",1,42,7,3,"end_turn",false),Contracts.Json));
    }
}
