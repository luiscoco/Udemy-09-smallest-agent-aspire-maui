$ErrorActionPreference = 'Stop'
Push-Location $PSScriptRoot
try {
    # Direct local launch; the Aspire dashboard additionally supplies discovery
    # and OTLP settings when launching its registered Windows target.
    dotnet build src/SmallestAgent.Maui -t:Run -f net10.0-windows10.0.19041.0
} finally { Pop-Location }
