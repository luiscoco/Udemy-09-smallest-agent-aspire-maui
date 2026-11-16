# The Smallest Agent — Aspire + MAUI Blazor Hybrid + .NET 10 Web API

Independent edition 09 with **Aspire 13.6.4**, a real Windows **MAUI Blazor Hybrid** app, a **.NET 10 Web API**, a shared Razor UI and a browser companion. It retains the lab, five lesson sections, readable pastel cards, dark theme, colored code, safe Markdown and 16 source-linked UML interactions.

The supplied [C# documentation](https://platform.claude.com/docs/en/cli-sdks-libraries/sdks/csharp) describes the official **Anthropic C# Messages SDK**, used only by the API. This is one model turn with no tools; it does not claim to be the separate CLI-backed Python/TypeScript Claude Agent SDK. TextDelta and ResultMessage are application event labels. Actual input/output tokens and stop reason are shown; USD cost is not reported rather than invented.

## Versions and prerequisites

Verified on Windows with .NET SDK **10.0.300**, runtime **10.0.8**, and installed **maui-windows 10.0.20** workload. The native target is net10.0-windows10.0.19041.0, with Windows 10 1809 minimum and the WebView2 runtime. Native Android/iOS/Mac targets are outside this edition.

NuGet's stable package index listed **Aspire.Hosting.AppHost/Aspire.AppHost.Sdk 13.6.4** as latest stable on 9 November 2026. The official MAUI integration remains preview and is pinned to **Aspire.Hosting.Maui 13.6.4-preview.1.26523.11**. MAUI packages match the installed 10.0.20 workload. The official Anthropic package is pinned to 13.6.0. Restore lockfiles record resolved dependencies.

This solution runs project resources without containers, so Docker is unnecessary for its current graph. The AppHost SDK uses its matching Aspire CLI bundle; a separate global Aspire CLI installation is not required. No Python or Node.js runtime is required by the application. PowerShell 7 runs the asset generator/helpers.

## Windows Terminal — setup

From **Lesson 0**:

```powershell
cd .\09-smallest-agent-aspire-maui
dotnet --version
dotnet workload list
dotnet restore SmallestAgent.slnx --configfile NuGet.Config
if (!(Test-Path .env)) { Copy-Item .env.example .env }
notepad .env
```

If the Windows MAUI workload is absent, install the MAUI/WinUI workload using Visual Studio Installer, or use `dotnet workload install maui-windows` in an appropriately permitted terminal. WebView2 must be installed for the native WebView. This machine already has the required workload.

Set **this project's** root .env and save it before starting:

```dotenv
ANTHROPIC_API_KEY=your_actual_api_key
CLAUDE_MODEL=claude-sonnet-5-5
AGENT_MAX_TOKENS=1024
AGENT_SYSTEM_PROMPT=You are a helpful assistant. Answer clearly and concisely.
```

Select a model available to your Anthropic account. Max output tokens accept 1–8192. Root .env.local overrides .env; ASP.NET configuration (process variables, user secrets, command line) overrides file values. Only the API loads these files; no key is forwarded to client resources. Restart the API after changes. Real environment files are ignored and were not created/copied by the implementation. Keep keys out of MAUI, client appsettings, screenshots and source control.

Optional API-only user secrets:

```powershell
dotnet user-secrets set "ANTHROPIC_API_KEY" "your_actual_api_key" --project src/SmallestAgent.Api
```

## Run Aspire

Stay in **09-smallest-agent-aspire-maui**:

```powershell
.\Start-Application.ps1
```

Equivalent: `dotnet run --project src/SmallestAgent.AppHost --launch-profile http`. The helper locates its own project and regenerates teaching assets before launch.

- Browser companion: **http://127.0.0.1:5000**
- Web API Swagger: **http://127.0.0.1:8400/docs**
- OpenAPI: **http://127.0.0.1:8400/openapi/v1.json** (also /openapi.json redirect)
- Aspire dashboard: **http://127.0.0.1:18889** — use the session login URL printed in the terminal.

Keep the AppHost running. Ctrl+C stops its managed resources. The launch profile permits HTTP transport for these local development endpoints; dashboard login remains enabled. Do not copy a session login token into documentation or screenshots.

The native Windows resource is **explicit-start**, not automatically opened. Launch its Windows target from the Aspire dashboard; that path supplies service-discovery and OTLP configuration. Alternatively, while Aspire is running:

```powershell
.\Start-Hybrid.ps1
```

That direct launcher builds/runs the real native app against loopback API port 8400. It uses the API fallback address; the dashboard launch supplies discovery and telemetry. `AgentApi__BaseUrl` can override the native fallback. The window renders shared Razor components inside BlazorWebView; it is not a WebView pointed at the browser website. Native clipboard and Markdown export use MAUI clipboard and a Windows file-save picker.

## Projects and boundaries

- **SmallestAgent.AppHost**: latest stable Aspire SDK, managed API/web resources, explicit-start MAUI Windows target, references, health gating and endpoint registration. Do not add a ProjectReference from AppHost to the incompatible native MAUI target; official AddMauiProject uses its csproj path.
- **SmallestAgent.Api**: .NET 10 Web API, private configuration, validation, native Swagger/OpenAPI, official C# SDK and safe SSE events.
- **SmallestAgent.Shared**: public contracts, typed HTTP/SSE client, sanitized/highlighted presentation, embedded teaching excerpts, Razor routes/lab/lessons and static assets. No SDK or secret-loading imports.
- **SmallestAgent.Web**: interactive server browser companion using the shared RCL. Static web assets are explicitly loaded; JavaScript interop handles theme and browser export, while model requests use the C# client.
- **SmallestAgent.Maui**: Windows MAUI app with native BlazorWebView and native host actions. Only client settings/service endpoints belong here.
- **SmallestAgent.ServiceDefaults / MauiServiceDefaults**: service discovery, health/telemetry and OTLP export. No automatic model POST retries; SDK retries are also disabled to avoid duplicate paid calls.
- **SmallestAgent.Checks**: executable meaningful tests with mocked HTTP/provider boundaries plus real interactive shared-component rendering.

## Build and verify

From this edition's root:

```powershell
pwsh -File scripts/Generate-Lesson.ps1
dotnet build SmallestAgent.slnx
dotnet run --project tests/SmallestAgent.Checks
```

Stop the AppHost before rebuilding its running projects to avoid Windows assembly locks. The source generator synchronizes all 16 SVG labels, numbered cards and actual C# excerpts, plus an AppHost orchestration excerpt. Regenerate after source changes. Local SVG/PlantUML downloads need no hosted rendering service.

The legacy Windows XAML compiler encountered MAX_PATH in this workspace: its intermediate assembly path was 266 characters. Directory.Build.props therefore stores **native MAUI generated artifacts** under `%TEMP%\SmallestAgent09` by default. Source stays in this folder, and MSBuild's Run target follows the configured output. Override `HybridArtifactsRoot` with a short path ending in a separator when necessary; do not copy the source into another project to build it. The native executable is under that root's bin/Debug/net10.0-windows10.0.19041.0/win-x64 directory.

Health reports connectivity and key presence, not authentication, credits or model access. Query mode returns safe 503 without a key and 502 on provider failure; Stream returns error without done after headers. Input JSON/type/origin/body/prompt failures produce 400/415/403/413/422 before model work. Prompts are trimmed to 1–4000 characters and actual body bytes bounded at 32 KiB. Stream framing supports UTF-8, CRLF, comments and multiline data. A final result plus done are required. The API sends heartbeats, applies a three-minute deadline, and cancels/finishes iteration before disposing the SDK stream. Provider text is redacted across chunk boundaries; raw SDK/thinking/session objects are not forwarded.

See VERIFICATION.md for actual results and limitations and verification/ for review screenshots. Optional scripts/check_browser.py uses Python Playwright only for QA, refuses a configured API before sending any prompt, and is not a runtime dependency. BUILD-PROMPT.txt is the complete standalone recreation prompt; an identical copy sits beside the other root prompts.

## References

- [Aspire packages](https://www.nuget.org/packages/Aspire.Hosting.AppHost/13.6.4)
- [Official Aspire MAUI integration](https://aspire.dev/integrations/dotnet/maui/)
- [MAUI Hybrid/shared Razor guidance](https://learn.microsoft.com/aspnet/core/blazor/hybrid/tutorials/maui-blazor-web-app?view=aspnetcore-10.0)
- [Official C# SDK](https://platform.claude.com/docs/en/cli-sdks-libraries/sdks/csharp)
