namespace SmallestAgent.Api.Services;

using SmallestAgent.Shared.Services;

public sealed record AgentSettings(string ApiKey, string Model, int MaxTokens, string SystemPrompt)
{
    public bool Configured => !string.IsNullOrWhiteSpace(ApiKey) && ApiKey != "sk-ant-replace-with-your-key";
    public static AgentSettings Load(IConfiguration configuration, string contentRoot)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        var parent = Directory.GetParent(contentRoot);
        var root = parent?.Name == "src" ? parent.Parent!.FullName : contentRoot;
        foreach (var name in new[] { ".env", ".env.local" })
        {
            var path = Path.Combine(root, name);
            if (!File.Exists(path)) continue;
            foreach (var raw in File.ReadLines(path))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith('#')) continue;
                var index = line.IndexOf('=');
                if (index <= 0) continue;
                values[line[..index].Trim()] = line[(index + 1)..].Trim().Trim('"', '\'');
            }
        }
        string Read(string key, string fallback = "") => configuration[key] ?? values.GetValueOrDefault(key, fallback);
        if (!int.TryParse(Read("AGENT_MAX_TOKENS", "1024"), out var tokens) || tokens is < 1 or > 8192)
            throw new InvalidOperationException("AGENT_MAX_TOKENS must be an integer from 1 to 8192.");
        return new(Read("ANTHROPIC_API_KEY").Trim(), Read("CLAUDE_MODEL", "claude-sonnet-5-5"), tokens,
            Read("AGENT_SYSTEM_PROMPT", "You are a helpful assistant. Answer clearly and concisely."));
    }
}
