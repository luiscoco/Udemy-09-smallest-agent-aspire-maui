using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
namespace SmallestAgent.Shared.Services;
public sealed record HealthInfo(string Status, bool ApiKeyConfigured);
public sealed record ClientMessage(string Type, string Summary, JsonElement Data);
public interface IAgentApiClient
{
    Task<HealthInfo> HealthAsync(CancellationToken token = default);
    Task<AgentResult> QueryAsync(string prompt, CancellationToken token);
    IAsyncEnumerable<ClientMessage> StreamAsync(string prompt, CancellationToken token);
}
public sealed class AgentApiException(string message) : Exception(message);
public sealed class AgentApiClient(HttpClient http) : IAgentApiClient
{
    public async Task<HealthInfo> HealthAsync(CancellationToken token = default) =>
        await http.GetFromJsonAsync<HealthInfo>("/api/health", Contracts.Json, token) ?? throw new AgentApiException("Invalid health response.");
    public async Task<AgentResult> QueryAsync(string prompt, CancellationToken token)
    {
        using var response = await http.PostAsJsonAsync("/api/agent/query", new PromptRequest(prompt), Contracts.Json, token);
        await EnsureSuccess(response, token);
        return await response.Content.ReadFromJsonAsync<AgentResult>(Contracts.Json, token) ?? throw new AgentApiException("The API returned no final result.");
    }
    public async IAsyncEnumerable<ClientMessage> StreamAsync(string prompt, [EnumeratorCancellation] CancellationToken token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/agent/stream") { Content = JsonContent.Create(new PromptRequest(prompt), options: Contracts.Json) };
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
        await EnsureSuccess(response, token);
        await using var stream = await response.Content.ReadAsStreamAsync(token);
        var finalReceived = false; var done = false;
        await foreach (var frame in ReadEvents(stream, token))
        {
            if (frame.Event == "error") throw new AgentApiException(Detail(frame.Data, "The response stream failed."));
            if (frame.Event == "message")
            {
                var message = frame.Data.Deserialize<ClientMessage>(Contracts.Json) ?? throw new AgentApiException("Invalid message.");
                if (message.Type is not ("SystemMessage" or "TextDelta" or "ResultMessage")) throw new AgentApiException("Invalid message type.");
                if (message.Type == "ResultMessage") { ValidateResult(message.Data); finalReceived = true; }
                yield return message;
            }
            if (frame.Event == "done") { done = true; break; }
        }
        if (!finalReceived || !done) throw new AgentApiException("The stream closed before the final answer was confirmed. Please try again.");
    }
    public static async IAsyncEnumerable<(string Event, JsonElement Data)> ReadEvents(Stream stream, [EnumeratorCancellation] CancellationToken token = default)
    {
        // StreamReader keeps partial UTF-8 codepoints; ReadLineAsync handles CRLF.
        using var reader = new StreamReader(stream, Encoding.UTF8, true, 4096, leaveOpen: true);
        var eventName = "message"; var data = new List<string>();
        while (await reader.ReadLineAsync(token) is { } line)
        {
            if (line.Length == 0)
            {
                if (data.Count > 0) { using var json = JsonDocument.Parse(string.Join('\n', data)); yield return (eventName, json.RootElement.Clone()); }
                eventName = "message"; data.Clear();
            }
            else if (line.StartsWith("event:", StringComparison.Ordinal)) eventName = line[6..].Trim();
            else if (line.StartsWith("data:", StringComparison.Ordinal)) data.Add(line[5..].TrimStart(' '));
        }
    }
    private static void ValidateResult(JsonElement data)
    {
        if (!data.TryGetProperty("result", out var text) || text.ValueKind != JsonValueKind.String ||
            !data.TryGetProperty("num_turns", out var turns) || !turns.TryGetInt32(out _) ||
            !data.TryGetProperty("duration_ms", out var duration) || !duration.TryGetInt64(out _) ||
            !data.TryGetProperty("is_error", out var error) || error.ValueKind is not (JsonValueKind.True or JsonValueKind.False)) throw new AgentApiException("Invalid final result.");
    }
    private static string Detail(JsonElement data, string fallback) => data.ValueKind == JsonValueKind.Object && data.TryGetProperty("detail", out var detail) && detail.ValueKind == JsonValueKind.String ? detail.GetString()! : fallback;
    private static async Task EnsureSuccess(HttpResponseMessage response, CancellationToken token)
    {
        if (response.IsSuccessStatusCode) return;
        var fallback = $"The API request failed (HTTP {(int)response.StatusCode}).";
        try { using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(token)); throw new AgentApiException(Detail(json.RootElement, fallback)); }
        catch (JsonException) { throw new AgentApiException(fallback); }
    }
}
