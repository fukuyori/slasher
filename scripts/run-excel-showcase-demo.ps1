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

function ConvertTo-WorkspaceRelativePath {
    param(
        [string]$WorkspaceRoot,
        [string]$Path
    )

    $resolvedRoot = [IO.Path]::GetFullPath($WorkspaceRoot).TrimEnd('\', '/')
    $resolvedPath = [IO.Path]::GetFullPath($Path)
    if (!$resolvedPath.StartsWith($resolvedRoot, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Path is outside workspace: $Path"
    }

    return $resolvedPath.Substring($resolvedRoot.Length).TrimStart('\', '/') -replace '\\', '/'
}

function New-ExcelShowcaseNumadoraScript {
    param(
        [string]$Path,
        [string]$BindingsRoot,
        [int]$ScreenIndex
    )

    $captureLine = if ($ScreenIndex -ge 0) {
        "    screen.CaptureMonitor($ScreenIndex, 1440, 810)"
    } else {
        '    workbook.Capture(1440, 810)'
    }
    $screenImport = if ($ScreenIndex -ge 0) {
        "IMPORT slasher_screen AS screen`r`n"
    } else {
        ""
    }

    $script = @"
IMPORT slasher_desktop AS desktop
${screenImport}IMPORT slasher_io AS io

FUNC main()
    io.Step("open workbook")
    LET excel := desktop.StartApp("artifacts/demo/workbook-app.xlsx")

    io.Step("wait for workbook window")
    LET workbook := excel.WaitForWindow("workbook-app", 20000)

    io.Step("maximize workbook")
    workbook.Maximize()
    io.Wait(800)

    io.Step("capture workbook")
$captureLine
    io.Wait(5000)

    io.Step("close Excel")
    excel.Close()
END
"@

    Set-Content -Path $Path -Value $script -Encoding UTF8
    Copy-Item -Path (Join-Path $BindingsRoot "slasher_*.numa") -Destination (Split-Path -Parent $Path) -Force
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
$workspaceRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$workbookPath = Resolve-Path (Join-Path $demoRoot "workbook.xlsx")
$readWorkbookPath = Join-Path $demoRoot "workbook-read.xlsx"
$appWorkbookPath = Join-Path $demoRoot "workbook-app.xlsx"
$numadoraScriptPath = Join-Path $demoRoot "excel-showcase-run.numa"
$capturePath = Join-Path $demoRoot "excel-showcase-capture.bmp"
$summaryPath = Join-Path $demoRoot "excel-showcase-summary.json"

Write-Host "Running Slasher Excel application showcase..."

try {
    Copy-Item -LiteralPath $workbookPath -Destination $readWorkbookPath -Force
} catch {
    Write-Warning "Could not refresh workbook-read.xlsx; using existing copy if available. $($_.Exception.Message)"
}

$readPath = if (Test-Path $readWorkbookPath) { "artifacts/demo/workbook-read.xlsx" } else { "artifacts/demo/workbook.xlsx" }
$excel = Invoke-SlasherJson -Method Post -Path "/data/excel/read" -Body @{
    path = $readPath
    hasHeader = $true
}
Write-Host "  Read workbook data through Slasher API: $($excel.rows.Count) rows"

try {
    Copy-Item -LiteralPath $workbookPath -Destination $appWorkbookPath -Force
} catch {
    Write-Warning "Could not refresh workbook-app.xlsx; using existing copy if available. $($_.Exception.Message)"
    if (!(Test-Path $appWorkbookPath)) {
        throw
    }
}

New-ExcelShowcaseNumadoraScript `
    -Path $numadoraScriptPath `
    -BindingsRoot (Join-Path $PSScriptRoot "numadora-samples") `
    -ScreenIndex $ScreenIndex
$numadoraScriptRelativePath = ConvertTo-WorkspaceRelativePath -WorkspaceRoot $workspaceRoot -Path $numadoraScriptPath
Write-Host "  Running Numadora script: $numadoraScriptRelativePath"

$run = Invoke-SlasherJson -Method Post -Path "/scripts/run-file" -Body @{
    path = $numadoraScriptRelativePath
    name = "Excel showcase demo"
    language = "numadora"
    purpose = "showcase-demo"
    allowInteractiveInput = $true
    capturePolicy = @{
        captureOnError = $true
        captureAfterEachStep = $false
        captureTarget = "selected"
    }
}

if (-not $run.ok) {
    throw "Numadora Excel showcase failed: $($run.error.code) $($run.error.message)"
}

$captureEvidence = $run.events |
    ForEach-Object { $_.evidence } |
    Where-Object { $_ -and $_.kind -eq "screenshot" } |
    Select-Object -Last 1

if ($null -ne $captureEvidence) {
    Copy-Item -LiteralPath (Join-Path $workspaceRoot $captureEvidence.path) -Destination $capturePath -Force
    Write-Host "  Saved Excel screenshot: $capturePath"
}

$summary = [ordered]@{
    timestamp = (Get-Date).ToString("o")
    workbook = $workbookPath.Path
    openedWorkbook = $appWorkbookPath
    script = [ordered]@{
        language = "numadora"
        path = $numadoraScriptRelativePath
    }
    runId = $run.run.runId
    status = $run.run.status
    report = $run.run.artifacts.report
    screenshot = [ordered]@{
        path = $capturePath
        evidence = $captureEvidence.path
        width = $captureEvidence.width
        height = $captureEvidence.height
        screenIndex = if ($ScreenIndex -ge 0) { $ScreenIndex } else { $null }
    }
    data = [ordered]@{
        sheet = $excel.sheet
        rows = $excel.rows.Count
        headers = $excel.headers
    }
}

$summary | ConvertTo-Json -Depth 8 | Set-Content -Path $summaryPath -Encoding UTF8

Write-Host "  Saved summary: $summaryPath"
Write-Host "Slasher Excel application showcase completed."
