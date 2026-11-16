using System.Text.Json;
using Microsoft.AspNetCore.Http.Features;

namespace SmallestAgent.Api.Services;

using SmallestAgent.Shared.Services;

public static class AgentEndpoints
{
    public static void MapAgentEndpoints(this WebApplication app)
    {
        app.MapGet("/api/health", (AgentSettings settings) => Results.Json(new { status = "ok", api_key_configured = settings.Configured, sdk = "Anthropic C# Messages SDK" }));
        app.MapPost("/api/agent/query", async (HttpContext context, AgentSettings settings, IAgentRunner agent) =>
        {
            var input = await ReadPrompt(context);
            if (input.Error is not null) return input.Error;
            if (!settings.Configured) return Results.Json(new { detail = Contracts.MissingKey }, statusCode: 503);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);
            timeout.CancelAfter(TimeSpan.FromMinutes(3));
            try { return Results.Json(await agent.QueryAsync(input.Prompt!, timeout.Token), Contracts.Json); }
            catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested) { return Results.StatusCode(499); }
            catch { return Results.Json(new { detail = Contracts.Failure }, statusCode: 502); }
        }).Accepts<PromptRequest>("application/json").Produces<AgentResult>().Produces(400).Produces(403).Produces(413).Produces(415).Produces(422).Produces(502).Produces(503);
        app.MapPost("/api/agent/stream", async (HttpContext context, AgentSettings settings, IAgentRunner agent) =>
        {
            var input = await ReadPrompt(context);
            if (input.Error is not null) { await input.Error.ExecuteAsync(context); return; }
            context.Response.ContentType = "text/event-stream";
            context.Response.Headers.CacheControl = "no-cache, no-transform";
            context.Response.Headers["X-Accel-Buffering"] = "no";
            context.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);
            timeout.CancelAfter(TimeSpan.FromMinutes(3));
            async Task Send(string name, object value)
            {
                await context.Response.WriteAsync($"event: {name}\ndata: {JsonSerializer.Serialize(value, Contracts.Json)}\n\n", context.RequestAborted);
                await context.Response.Body.FlushAsync(context.RequestAborted);
            }
            if (!settings.Configured) { await Send("error", new { detail = Contracts.MissingKey }); return; }
            var finalReceived = false;
            try
            {
                await using var iterator = agent.StreamAsync(input.Prompt!, timeout.Token).GetAsyncEnumerator(timeout.Token);
                var next = iterator.MoveNextAsync().AsTask();
                var heartbeat = Task.Delay(TimeSpan.FromSeconds(15), timeout.Token);
                try
                {
                    while (true)
                    {
                        if (await Task.WhenAny(next, heartbeat) != next)
                        {
                            timeout.Token.ThrowIfCancellationRequested();
                            await context.Response.WriteAsync(": keep-alive\n\n", timeout.Token);
                            await context.Response.Body.FlushAsync(timeout.Token);
                            heartbeat = Task.Delay(TimeSpan.FromSeconds(15), timeout.Token);
                            continue;
                        }
                        if (!await next) break;
                        var message = iterator.Current;
                        if (message.Data is AgentResult) finalReceived = true;
                        await Send("message", message);
                        next = iterator.MoveNextAsync().AsTask();
                    }
                }
                finally
                {
                    timeout.Cancel();
                    // Finish outstanding iteration before disposing the async
                    // iterator; concurrent DisposeAsync/MoveNext is invalid.
                    try { await next; } catch { }
                }
                if (!finalReceived) throw new InvalidOperationException("Missing final result.");
                await Send("done", new { status = "complete" });
            }
            catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested) { }
            catch { if (!context.RequestAborted.IsCancellationRequested) await Send("error", new { detail = Contracts.Failure }); }
        }).Accepts<PromptRequest>("application/json").Produces(200, contentType: "text/event-stream").Produces(400).Produces(403).Produces(413).Produces(415).Produces(422);
    }
    private static async Task<(string? Prompt, IResult? Error)> ReadPrompt(HttpContext context)
    {
        IResult Error(int status, string detail) => Results.Json(new { detail }, statusCode: status);
        var request = context.Request;
        var origin = request.Headers.Origin.ToString();
        // Host retains the browser authority; do not trust unconfigured forwarded headers.
        if (origin.Length > 0 && origin != $"{request.Scheme}://{request.Host}") return (null, Error(403, "Origin is not allowed."));
        if (!request.HasJsonContentType()) return (null, Error(415, "Use Content-Type: application/json."));
        try
        {
            using var body = new MemoryStream();
            var buffer = new byte[4096];
            int read;
            while ((read = await request.Body.ReadAsync(buffer, context.RequestAborted)) > 0)
            {
                if (body.Length + read > 32768) return (null, Error(413, "Request body is too large."));
                body.Write(buffer, 0, read);
            }
            var value = JsonSerializer.Deserialize<PromptRequest>(body.ToArray(), Contracts.Json);
            var prompt = Contracts.ValidatePrompt(value?.Prompt);
            return prompt is null ? (null, Error(422, "Prompt must contain 1–4000 characters after trimming.")) : (prompt, null);
        }
        catch (BadHttpRequestException error) { return (null, Error(error.StatusCode, "Request body is too large or invalid.")); }
        catch (JsonException) { return (null, Error(400, "Request must contain valid JSON with a string prompt.")); }
    }
}
