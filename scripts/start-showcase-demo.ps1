param(
    [int]$Port = 5055,
    [switch]$NoOpen,
    [switch]$NoAutoRun
)

$ErrorActionPreference = "Stop"

& "$PSScriptRoot\prepare-demo-assets.ps1"

$url = "http://127.0.0.1:$Port/demo.html"
if (-not $NoAutoRun) {
    $url = "$url`?autorun=1"
}
$health = "http://127.0.0.1:$Port/health"

$alreadyRunning = $false
try {
    Invoke-RestMethod -Uri $health -TimeoutSec 2 | Out-Null
    $alreadyRunning = $true
} catch {
    $alreadyRunning = $false
}

if (-not $alreadyRunning) {
    $project = Join-Path $PSScriptRoot "..\src\Slasher\Slasher.csproj"
    $arguments = @(
        "run",
        "--project", $project,
        "--urls", "http://127.0.0.1:$Port"
    )

    Start-Process -FilePath "dotnet" -ArgumentList $arguments -WorkingDirectory (Resolve-Path (Join-Path $PSScriptRoot "..")) -WindowStyle Hidden

    $deadline = (Get-Date).AddSeconds(20)
    do {
        Start-Sleep -Milliseconds 500
        try {
            Invoke-RestMethod -Uri $health -TimeoutSec 2 | Out-Null
            $alreadyRunning = $true
        } catch {
            $alreadyRunning = $false
        }
    } while (-not $alreadyRunning -and (Get-Date) -lt $deadline)
}

if (-not $alreadyRunning) {
    throw "Slasher did not become ready at $health"
}

if (-not $NoOpen) {
    Start-Process $url
}

Write-Host "Slasher showcase demo is running:"
Write-Host "  $url"
