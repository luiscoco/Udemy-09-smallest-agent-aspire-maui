using System.Text.Json;

namespace SmallestAgent.Shared.Services;

public sealed record AgentResult(string Result, int NumTurns, long DurationMs, long InputTokens, long OutputTokens, string StopReason, bool IsError, decimal? TotalCostUsd = null);
public sealed record AgentMessage(string Type, string Summary, object Data);
public sealed record PromptRequest(string? Prompt);
public interface IAgentRunner
{
    IAsyncEnumerable<AgentMessage> StreamAsync(string prompt, CancellationToken cancellationToken);
    Task<AgentResult> QueryAsync(string prompt, CancellationToken cancellationToken);
}
public static class Contracts
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };
    public const string MissingKey = "Configure ANTHROPIC_API_KEY in this application's root .env file, then restart the server.";
    public const string Failure = "Claude could not finish. Check your server API key, model access and connection, then try again.";
    public static string? ValidatePrompt(string? value) => value?.Trim() is { Length: >= 1 and <= 4000 } prompt ? prompt : null;
}
