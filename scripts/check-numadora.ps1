param(
    [Parameter(Mandatory = $true)]
    [string] $Path,

    [int] $Port = 5055
)

$ErrorActionPreference = "Stop"

function Ensure-SlasherServer {
    $health = "http://127.0.0.1:$Port/health"
    try {
        Invoke-RestMethod -Uri $health -TimeoutSec 2 | Out-Null
        return
    } catch {
    }

    $project = Join-Path $PSScriptRoot "..\src\Slasher\Slasher.csproj"
    $root = Resolve-Path (Join-Path $PSScriptRoot "..")
    $arguments = @(
        "run",
        "--project", $project,
        "--urls", "http://127.0.0.1:$Port"
    )

    Start-Process -FilePath "dotnet" -ArgumentList $arguments -WorkingDirectory $root -WindowStyle Hidden

    $deadline = (Get-Date).AddSeconds(20)
    do {
        Start-Sleep -Milliseconds 500
        try {
            Invoke-RestMethod -Uri $health -TimeoutSec 2 | Out-Null
            return
        } catch {
        }
    } while ((Get-Date) -lt $deadline)

    throw "Slasher did not become ready at $health"
}

$resolvedPath = Resolve-Path -LiteralPath $Path
$root = Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..")
$relativePath = [IO.Path]::GetRelativePath($root, $resolvedPath)

Ensure-SlasherServer

$body = @{
    path = $relativePath
    language = "numadora"
} | ConvertTo-Json -Depth 8

try {
    $response = Invoke-RestMethod `
        -Uri "http://127.0.0.1:$Port/scripts/check" `
        -Method Post `
        -ContentType "application/json" `
        -Body $body
    Write-Host "Numadora check passed: $relativePath"
    $response.requiredCapabilities | ConvertTo-Json -Depth 8
} catch {
    if ($_.ErrorDetails.Message) {
        Write-Host $_.ErrorDetails.Message
    }

    exit 1
}
