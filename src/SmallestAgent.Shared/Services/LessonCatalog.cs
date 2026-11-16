using System.Text.Json;
namespace SmallestAgent.Shared.Services;
public sealed record LessonStep(int Number, string Title, string Source, string Language, string Text, string Code, string Explanation);
public sealed class LessonCatalog
{
    public IReadOnlyList<LessonStep> Steps { get; }
    public string OrchestrationCode { get; }
    public LessonCatalog()
    {
        using var source = typeof(LessonCatalog).Assembly.GetManifestResourceStream("SmallestAgent.Shared.Lessons.workflow.json") ?? throw new InvalidOperationException("Generate lesson excerpts before building.");
        Steps = JsonSerializer.Deserialize<List<LessonStep>>(source, new JsonSerializerOptions(JsonSerializerDefaults.Web)) ?? throw new InvalidOperationException("Invalid lesson data.");
        using var orchestration = typeof(LessonCatalog).Assembly.GetManifestResourceStream("SmallestAgent.Shared.Lessons.orchestration.json")!;
        using var document = JsonDocument.Parse(orchestration);
        OrchestrationCode = document.RootElement.GetProperty("code").GetString()!;
    }
}
public sealed record HostInfo(string Name, string ApiDocsUrl);
