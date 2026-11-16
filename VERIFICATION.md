# Verification — 9 November 2026

## Versions

Verified latest stable Aspire package versions directly from NuGet: Aspire.AppHost.Sdk and Aspire.Hosting.AppHost **13.6.4**. Official MAUI hosting is still a preview integration, pinned to **13.6.4-preview.1.26523.11**. The application uses the official **Anthropic C# Messages SDK 13.6.0**, accurately distinguished from the Python/TypeScript Agent SDK.

Windows toolchain: .NET SDK **10.0.300**, runtime **10.0.8**, installed **maui-windows 10.0.20** and matching MAUI packages. Resolved package lockfiles are included for every project.

## Build and automated checks

- `dotnet restore SmallestAgent.slnx --locked-mode --configfile NuGet.Config`: projects restored successfully.
- `dotnet build SmallestAgent.slnx --no-restore -v minimal -tl:off`: **all eight projects built, 0 warnings and 0 errors**, including the real Windows MAUI target, API, browser host, latest Aspire AppHost, both defaults projects, shared RCL and checks.
- `dotnet run --project tests/SmallestAgent.Checks --no-restore --no-build`: **26 meaningful checks passed**.
- Coverage includes actual body-size/JSON/type/prompt/origin checks before provider execution, safe missing-key/failure contracts, SSE order/final-result enforcement, real C# SDK parsing over mocked HTTP, no-tools options, max_tokens early stop, redaction across text-delta boundaries, cancellation at the SDK HTTP boundary, safe highlighted code/Markdown, configuration bounds, shared-component rendering, URI-change navigation, typed UTF-8/CRLF/multiline SSE parsing, safe client errors and an interactive shared-component run with retained prompt across all five sections.
- The Windows XAML compiler failed while accessing a **266-character** intermediate assembly path. Moving only native generated artifacts to the short, configurable `%TEMP%\SmallestAgent09` location resolved the issue; the same source then compiled cleanly. Source was not relocated.
- Aspire's CLI bundle is enabled, resolving the initial ASPIRE010 configuration warning.
- Generated workflow and orchestration excerpts are deterministic; all 16 numbered labels/source markers match, the SVG has five lifelines, root/embedded build prompts are byte-identical, and no private environment content was copied into source or teaching assets.

## Actual Aspire and browser checks

- Started the exact root `Start-Application.ps1` helper. Aspire launched the separate API and browser resources, gated the browser on API health and supplied service discovery. The dashboard responded on 18889; its session login token was not saved in source, documentation or screenshots.
- Verified API health, readiness, real Swagger/generated OpenAPI, unknown API 404, input 400/403/413/415/422, no-key query 503 and stream error without done through real HTTP on port 8400.
- Browser startup testing caught zero-byte static/framework responses that left controls disabled. Explicit static-web-asset loading/static-file serving and fingerprint-aware framework/CSS references fixed the issue. The final companion became interactive and its assets were nonempty with correct MIME types.
- Chrome checks against the actual Aspire-managed companion on port 5000 passed: API connectivity, all five real link clicks and active links, retained prompt, all 16 highlighted source cards, UML SVG download, Stream/Simple missing-key feedback, Ctrl+Enter, enabled retry controls, persistent theme after reload and a 430-pixel layout without page overflow.
- Review screenshots: `verification/desktop.png`, `mobile.png` and `workflow.png`.

## Actual native Windows checks

- Launched the compiled **SmallestAgent.Maui.exe** with a hidden startup request and an ephemeral local WebView inspection port. The native process stayed running and initialized the real BlazorWebView at its local virtual host.
- A clean isolated native run passed checks through the native WebView's local inspection endpoint: shared lab initialization, actual API connectivity and missing-key presence, all five teaching sections/active links, all 16 architecture cards, retained prompt on return and the real typed-client missing-key Stream error.
- `verification/native-hybrid.png` captures the actual native WebView content. The browser companion was not used as a substitute for this native runtime check.
- Native clipboard and Windows file-save picker are compiled/wired through platform services but were deliberately **not invoked**. No user clipboard content was replaced and no OS save dialog was opened.
- Temporary WebView inspection arguments were passed only to verification processes. They are not in the application's shipped launch configuration.

## Limits and cleanup

No real API key was read/copied from another edition or created by the implementation. A real environment file present at handoff was left unread and untouched; verification had already run with the API reporting an unconfigured key. Mocked transport tests spend no credits; real native/browser requests exercised the missing-key path. Live Claude authentication, account model access, credits and successful inference were not tested. This edition verifies Windows native packaging/build/runtime; Android/iOS/Mac targets are outside its scope.

Optional browser/native QA used Python Playwright from an existing testing environment; Python is not an application dependency. All verification native processes and Aspire-managed resources owned by this task were stopped. No source-control operations were performed.
