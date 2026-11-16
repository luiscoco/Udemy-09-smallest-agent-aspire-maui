var builder = DistributedApplication.CreateBuilder(args);
var api = builder.AddProject<Projects.SmallestAgent_Api>("agent-api", launchProfileName: null)
    .WithHttpEndpoint(port: 8400, name: "http", isProxied: false)
    .WithHttpHealthCheck("/health");
builder.AddProject<Projects.SmallestAgent_Web>("lesson-web", launchProfileName: null)
    .WithHttpEndpoint(port: 5000, name: "http", isProxied: false)
    .WithReference(api).WaitFor(api);
// The integration registers a native Windows target. Launch it explicitly
// from the dashboard; the AppHost does not reference its incompatible TFM.
var hybrid = builder.AddMauiProject("lesson-hybrid", "../SmallestAgent.Maui/SmallestAgent.Maui.csproj");
hybrid.AddWindowsDevice().WithReference(api).WaitFor(api);
builder.Build().Run();
