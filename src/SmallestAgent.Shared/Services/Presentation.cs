using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Ganss.Xss;
using Markdig;

namespace SmallestAgent.Shared.Services;

public static partial class Presentation
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().DisableHtml().Build();
    [GeneratedRegex("(?<comment>//[^\\r\\n]*|#[^\\r\\n]*)|(?<str>\"(?:\\\\.|[^\"\\\\])*\"|'(?:\\\\.|[^'\\\\])*')|(?<keyword>\\b(?:using|namespace|public|private|static|sealed|class|record|interface|async|await|foreach|var|new|return|if|else|throw|try|catch|finally|true|false|null|void|string|int|long|bool|object|Task|CancellationToken|const|let|export|function|import|from|while|break|yield|in)\\b)|(?<number>\\b\\d+(?:\\.\\d+)?\\b)")]
    private static partial Regex Tokens();
    public static string Highlight(string source)
    {
        var html = new StringBuilder();
        var position = 0;
        foreach (Match token in Tokens().Matches(source))
        {
            html.Append(WebUtility.HtmlEncode(source[position..token.Index]));
            var name = token.Groups["comment"].Success ? "comment" : token.Groups["str"].Success ? "string" : token.Groups["number"].Success ? "number" : "keyword";
            html.Append($"<span class=\"hljs-{name}\">{WebUtility.HtmlEncode(token.Value)}</span>");
            position = token.Index + token.Length;
        }
        return html.Append(WebUtility.HtmlEncode(source[position..])).ToString();
    }
    public static string MarkdownHtml(string source)
    {
        var sanitizer = new HtmlSanitizer();
        sanitizer.AllowedTags.Remove("img");
        sanitizer.AllowedAttributes.Remove("style");
        var safe = sanitizer.Sanitize(Markdown.ToHtml(source, Pipeline));
        return Regex.Replace(safe, "<pre><code[^>]*>([\\s\\S]*?)</code></pre>", m => $"<pre><code>{Highlight(WebUtility.HtmlDecode(m.Groups[1].Value))}</code></pre>");
    }
}
