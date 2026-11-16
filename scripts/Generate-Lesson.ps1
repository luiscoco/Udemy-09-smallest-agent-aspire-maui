$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$app = $root
function Excerpt([string]$file, [string]$marker, [int]$count = 5) {
    $lines = [System.IO.File]::ReadAllLines((Join-Path $app $file))
    $index = -1
    for ($i = 0; $i -lt $lines.Length; $i++) { if ($lines[$i].Contains($marker)) { $index = $i; break } }
    if ($index -lt 0) { throw "Missing source marker: $file :: $marker" }
    $end = [Math]::Min($index + $count, $lines.Length)
    $selected = $lines[$index..($end - 1)]
    $indent = ($selected | Where-Object { $_.Trim().Length -gt 0 } | ForEach-Object { $_.Length - $_.TrimStart().Length } | Measure-Object -Minimum).Minimum
    return ($selected | ForEach-Object { if ($_.Length -ge $indent) { $_.Substring($indent) } else { '' } }) -join "`n"
}
$steps = [System.Collections.Generic.List[object]]::new()
function Step($number, $title, $file, $language, $marker, $count, $text, $explanation) {
    $steps.Add([ordered]@{ number=$number; title=$title; source=$file; language=$language; text=$text; code=(Excerpt $file $marker $count); explanation=$explanation })
}
Step 1 'Run agent (prompt)' 'src/SmallestAgent.Shared/Components/Pages/AgentLab.razor' 'csharp' 'var trimmed = Contracts.ValidatePrompt' 5 'The Blazor component trims the prompt and locks duplicate submissions before starting the request.' 'Neither client receives the API key. Busy state disables controls while a periodic timer measures elapsed time.'
Step 2 'POST /api/agent/stream' 'src/SmallestAgent.Shared/Services/AgentApiClient.cs' 'csharp' 'using var request = new HttpRequestMessage' 4 'A shared typed C# client posts JSON and requests response headers without buffering the SSE body.' 'The client runs natively in MAUI and on the Blazor server in the browser companion. Aspire supplies the API address through service discovery; neither client has the API key.' 
Step 3 'Validate; tools disabled' 'src/SmallestAgent.Api/Services/AgentEndpoints.cs' 'csharp' 'var origin = request.Headers.Origin' 5 'Validate the browser origin and JSON content type before reading the bounded body.' 'Host includes the browser port. Foreign origins are rejected without trusting forwarded headers. Prompt validation and tools configuration are shown in the server and SDK excerpts.'
Step 4 'CreateStreaming(parameters)' 'src/SmallestAgent.Api/Services/ClaudeAgentRunner.cs' 'csharp' 'var parameters = new MessageCreateParams' 6 'Build a typed C# request with a model, output limit, one user message and no tools.' 'This is the official Messages API client. It does not launch a CLI or automatically provide the separate Agent SDK loop.'
Step 5 'Prompt; no tools' 'src/SmallestAgent.Api/Services/ClaudeAgentRunner.cs' 'csharp' 'await foreach (var chunk in client.Messages.CreateStreaming' 6 'The C# SDK owns the HTTPS request to Claude and exposes its response as IAsyncEnumerable.' 'This iterator is the provider boundary in the diagram. The application does not hand-write a second model HTTP client.'
Step 6 'Model response' 'src/SmallestAgent.Api/Services/ClaudeAgentRunner.cs' 'csharp' 'if (type == "message_start"' 7 'Inspect typed SDK stream data and accumulate only text deltas and usage fields.' 'The excerpt reads the SDK union through its JSON converter. Raw SDK objects, IDs and thinking blocks are never forwarded.'
Step 7 'Safe text delta' 'src/SmallestAgent.Api/Services/ClaudeAgentRunner.cs' 'csharp' 'var text = delta.GetProperty("text")' 4 'Project a text delta into the public application event contract.' 'TextDelta is an application label, not an Agent SDK event class. The full answer is accumulated independently for the final result.'
Step 8 'SSE message event' 'src/SmallestAgent.Api/Services/AgentEndpoints.cs' 'csharp' 'async Task Send(string name' 5 'Encode named UTF-8 SSE frames and flush the response to the browser.' 'A blank line ends each event. Disabling buffering helps intermediate messages arrive promptly. The shared C# parser handles split bytes, Unicode and CRLF.'
Step 9 'Update typed state' 'src/SmallestAgent.Shared/Components/Pages/AgentLab.razor' 'csharp' 'private Task ReceiveMessage(ClientMessage message)' 9 'Each typed event updates the shared Razor component state.' 'InvokeAsync keeps rendering on the correct context in the native WebView or browser circuit. The visible timeline is bounded without losing the final result.' 
Step 10 'ResultMessage fields' 'src/SmallestAgent.Api/Services/ClaudeAgentRunner.cs' 'csharp' 'if (!complete || string.IsNullOrEmpty' 5 'Only a completed Claude stream becomes a normalized final result.' 'The one model turn, duration, token counts and stop reason come from this run. Non-final stops such as max_tokens are marked as errors. The Messages API does not report a USD cost.'
Step 11 'SSE ResultMessage' 'src/SmallestAgent.Api/Services/AgentEndpoints.cs' 'csharp' 'var message = iterator.Current;' 5 'Forward each safe event and remember whether a final result was observed.' 'A single outstanding MoveNextAsync also lets the API send heartbeat comments while inference pauses. Cancellation closes the iterator; missing final result is a failure.' 
Step 12 'Render answer + metrics' 'src/SmallestAgent.Shared/Services/Presentation.cs' 'csharp' 'public static string MarkdownHtml' 9 'Render Markdown with raw HTML disabled, sanitize it and highlight escaped code blocks.' 'The Blazor answer panel uses only this sanitized markup. Code tokens are HTML-encoded before colored spans are inserted. Result fields also populate the metric tiles.'
Step 13 'SSE done event' 'src/SmallestAgent.Shared/Services/AgentApiClient.cs' 'csharp' 'if (frame.Event == "done")' 3 'The C# SSE client requires both a final result and the terminal done event.' 'UTF-8 StreamReader and line framing handle split codepoints, CRLF and comments. Disposing the response closes transport. Completion never overrides a failed SDK result.' 
Step 14 'SSE error; no done' 'src/SmallestAgent.Api/Services/AgentEndpoints.cs' 'csharp' 'catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested) { }' 3 'After streaming starts, failures become an SSE error event without done.' 'Raw provider exceptions are kept out of the browser. Request disconnects cancel the SDK, and the linked token also enforces a three-minute deadline.'
Step 15 'Error + snackbar' 'src/SmallestAgent.Shared/Components/Pages/AgentLab.razor' 'csharp' 'catch (Exception exception) when' 5 'The component restores controls and shows a readable error state with a notification.' 'The typed client propagates safe server detail for HTTP failures, with a status-code fallback. Clipboard failures also have a user-visible fallback.'
Step 16 'Key: SSE error / input: 422' 'src/SmallestAgent.Api/Services/AgentEndpoints.cs' 'csharp' 'if (!settings.Configured) { await Send' 2 'Reject a missing key before any SDK iteration; invalid input is rejected before the SSE response begins.' 'Missing-key stream errors explain the root .env setup. Simple mode returns HTTP 503. Prompt validation produces 422 and neither path spends model credits.'
if ($steps.Count -ne 16) { throw 'Expected exactly 16 workflow steps.' }
$svg = [System.IO.File]::ReadAllText((Join-Path $PSScriptRoot 'workflow-template.svg'))
$labels = [regex]::Matches($svg, '<text[^>]*>(\d+)\. ([^<]+)</text>')
if ($labels.Count -ne 16 -or (@($labels | ForEach-Object { [int]$_.Groups[1].Value } | Sort-Object -Unique).Count -ne 16)) { throw 'Expected 16 SVG labels.' }
foreach ($step in $steps) {
    $label = $labels | Where-Object { [int]$_.Groups[1].Value -eq $step.number }
    if ([System.Net.WebUtility]::HtmlDecode($label.Groups[2].Value) -ne $step.title) { throw "Diagram mismatch: $($step.number)" }
}
New-Item -ItemType Directory -Path (Join-Path $app 'src/SmallestAgent.Shared/Lessons') -Force | Out-Null
[System.IO.File]::WriteAllText((Join-Path $app 'src/SmallestAgent.Shared/Lessons/workflow.json'), ($steps.ToArray() | ConvertTo-Json -Depth 8) + "`n")
[System.IO.File]::WriteAllText((Join-Path $app 'src/SmallestAgent.Shared/wwwroot/workflow.svg'), $svg)
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'workflow-template.puml') -Destination (Join-Path $app 'src/SmallestAgent.Shared/wwwroot/workflow.puml')
Write-Output 'Generated 16 matching workflow cards, UML SVG and PlantUML source.'
[System.IO.File]::WriteAllText((Join-Path $app 'src/SmallestAgent.Shared/Lessons/orchestration.json'), ([ordered]@{ code=[System.IO.File]::ReadAllText((Join-Path $app 'src/SmallestAgent.AppHost/Program.cs')) } | ConvertTo-Json) + "`n")
