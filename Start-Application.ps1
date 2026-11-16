$ErrorActionPreference = 'Stop'
Push-Location $PSScriptRoot
try {
    pwsh -File scripts/Generate-Lesson.ps1
    if ($LASTEXITCODE -ne 0) { throw 'Lesson asset generation failed.' }
    dotnet run --project src/SmallestAgent.AppHost --launch-profile http
} finally { Pop-Location }
