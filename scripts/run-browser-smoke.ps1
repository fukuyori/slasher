param(
    [string]$BaseUrl = "http://127.0.0.1:5055",
    [string]$TargetUrl = "",
    [string[]]$Browsers = @("edge", "chrome", "firefox"),
    [string]$ScriptTemplatePath = ".\scripts\samples\browser-smoke.slasher"
)

$ErrorActionPreference = "Stop"

$BrowserProcesses = @{
    edge = "msedge"
    chrome = "chrome"
    firefox = "firefox"
}

function Invoke-SlasherJson {
    param(
        [string]$Method,
        [string]$Path,
        [object]$Body = $null,
        [int]$TimeoutSec = 60
    )

    $uri = "$BaseUrl$Path"
    try {
        if ($null -eq $Body) {
            return Invoke-RestMethod -Method $Method -Uri $uri -TimeoutSec $TimeoutSec
        }

        return Invoke-RestMethod -Method $Method -Uri $uri -ContentType "application/json" -Body ($Body | ConvertTo-Json -Depth 12) -TimeoutSec $TimeoutSec
    } catch {
        if ($_.ErrorDetails.Message) { Write-Host $_.ErrorDetails.Message }
        throw
    }
}

try {
    Invoke-SlasherJson GET "/health" -TimeoutSec 5 | Out-Null
} catch {
    throw "Slasher server is not running. Start it first: dotnet run --project src\Slasher\Slasher.csproj --urls $BaseUrl"
}

if ([string]::IsNullOrWhiteSpace($TargetUrl)) {
    $TargetUrl = "$BaseUrl/index.html"
}

$template = Get-Content $ScriptTemplatePath -Raw
$results = @()

foreach ($browser in $Browsers) {
    $name = $browser.ToLowerInvariant()
    if (-not $BrowserProcesses.ContainsKey($name)) {
        throw "Unsupported browser '$browser'. Use edge, chrome, or firefox."
    }

    $processName = $BrowserProcesses[$name]
    $script = $template.
        Replace("__BROWSER__", $name).
        Replace("__PROCESS__", $processName).
        Replace("__URL__", $TargetUrl)

    Write-Host "Running browser smoke: $name -> $TargetUrl"
    try {
        $response = Invoke-SlasherJson POST "/scripts/run" @{
            script = $script
            name = "browser-$name-smoke"
            stopOnError = $true
        } -TimeoutSec 90

        $results += [pscustomobject]@{
            Browser = $name
            Ok = $response.ok
            Status = $response.run.status
            RunId = $response.run.runId
            ArtifactRoot = $response.run.artifactRoot
            Error = $null
        }
    } catch {
        $message = $_.Exception.Message
        if ($_.ErrorDetails.Message) {
            $message = $_.ErrorDetails.Message
        }

        $results += [pscustomobject]@{
            Browser = $name
            Ok = $false
            Status = "failed"
            RunId = $null
            ArtifactRoot = $null
            Error = $message
        }
    }
}

$results | Format-Table -AutoSize

$failed = @($results | Where-Object { -not $_.Ok })
if ($failed.Count -gt 0) {
    throw "$($failed.Count) browser smoke run(s) failed."
}
