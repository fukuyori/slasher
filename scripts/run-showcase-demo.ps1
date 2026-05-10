param(
    [int]$Port = 5055,
    [int]$ScreenIndex = -1,
    [switch]$ListScreens
)

$ErrorActionPreference = "Stop"

function Invoke-SlasherJson {
    param(
        [string]$Method,
        [string]$Path,
        [object]$Body = $null
    )

    $uri = "http://127.0.0.1:$Port$Path"
    if ($null -eq $Body) {
        return Invoke-RestMethod -Uri $uri -Method $Method
    }

    return Invoke-RestMethod -Uri $uri -Method $Method -ContentType "application/json" -Body ($Body | ConvertTo-Json -Depth 12)
}

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

function Save-Screenshot {
    param(
        [object]$Screenshot,
        [string]$Path
    )

    [IO.File]::WriteAllBytes($Path, [Convert]::FromBase64String($Screenshot.base64Image))
}

function Show-Screens {
    $screens = Invoke-SlasherJson -Method Get -Path "/screens"
    $screens |
        Select-Object index, deviceName, isPrimary,
            @{ Name = "bounds"; Expression = { "$($_.bounds.x),$($_.bounds.y) $($_.bounds.width)x$($_.bounds.height)" } },
            @{ Name = "workArea"; Expression = { "$($_.workArea.x),$($_.workArea.y) $($_.workArea.width)x$($_.workArea.height)" } } |
        Format-Table -AutoSize
}

if ($ListScreens) {
    Ensure-SlasherServer
    Show-Screens
    return
}

& "$PSScriptRoot\prepare-demo-assets.ps1"
Ensure-SlasherServer

$demoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\artifacts\demo")
$capturePath = Join-Path $demoRoot "showcase-capture.bmp"
$summaryPath = Join-Path $demoRoot "showcase-summary.json"

Write-Host "Running Slasher showcase without HTML..."

$started = Invoke-SlasherJson -Method Post -Path "/apps/start" -Body @{
    fileName = "notepad.exe"
}
Write-Host "  Started Notepad: $($started.processId)"

Start-Sleep -Milliseconds 800
$window = $null
if ($started.mainWindowHandle) {
    $window = Invoke-SlasherJson -Method Get -Path "/windows/$([uri]::EscapeDataString($started.mainWindowHandle))"
} else {
    $window = Invoke-SlasherJson -Method Post -Path "/windows/wait" -Body @{
        processName = "notepad"
        timeoutMs = 5000
    }
}

Invoke-SlasherJson -Method Post -Path "/windows/$([uri]::EscapeDataString($window.handle))/focus" -Body @{} | Out-Null
Invoke-SlasherJson -Method Post -Path "/input/text" -Body @{
    text = "Slasher demo: visible local automation"
} | Out-Null
Write-Host "  Typed demo text into: $($window.title)"

$screenshot = Invoke-SlasherJson -Method Post -Path "/screenshot" -Body @{
    maxWidth = 1440
    maxHeight = 810
    screenIndex = if ($ScreenIndex -ge 0) { $ScreenIndex } else { $null }
}
Save-Screenshot -Screenshot $screenshot -Path $capturePath
Write-Host "  Saved screenshot: $capturePath"

$csv = Invoke-SlasherJson -Method Post -Path "/data/csv/read" -Body @{
    path = "artifacts/demo/customers.csv"
    hasHeader = $true
}
$excel = Invoke-SlasherJson -Method Post -Path "/data/excel/read" -Body @{
    path = "artifacts/demo/workbook.xlsx"
    hasHeader = $true
}
$config = Invoke-SlasherJson -Method Post -Path "/data/json/query" -Body @{
    path = "artifacts/demo/config.json"
    pointer = "/source"
}
Write-Host "  Loaded data: $($csv.rows.Count) customers, $($excel.rows.Count) workbook rows"

$summary = [ordered]@{
    timestamp = (Get-Date).ToString("o")
    notepad = [ordered]@{
        processId = $started.processId
        handle = $window.handle
        title = $window.title
    }
    screenshot = [ordered]@{
        path = $capturePath
        width = $screenshot.width
        height = $screenshot.height
        screenIndex = if ($ScreenIndex -ge 0) { $ScreenIndex } else { $null }
    }
    data = [ordered]@{
        csvRows = $csv.rows.Count
        excelRows = $excel.rows.Count
        jsonSource = $config.value
    }
}

$summary | ConvertTo-Json -Depth 8 | Set-Content -Path $summaryPath -Encoding UTF8

Write-Host "  Saved summary: $summaryPath"
Write-Host "Slasher showcase demo completed."
