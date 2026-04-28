param(
    [string]$BaseUrl = "http://127.0.0.1:5055",
    [string]$ScriptPath = ".\scripts\samples\mouse-demo.slasher",
    [switch]$KeepApp
)

$ErrorActionPreference = "Stop"

function Invoke-SlasherJson {
    param(
        [string]$Method,
        [string]$Path,
        [object]$Body = $null
    )

    $uri = "$BaseUrl$Path"
    if ($null -eq $Body) {
        try {
            return Invoke-RestMethod -Method $Method -Uri $uri
        } catch {
            if ($_.ErrorDetails.Message) { Write-Host $_.ErrorDetails.Message }
            throw
        }
    }

    try {
        return Invoke-RestMethod -Method $Method -Uri $uri -ContentType "application/json" -Body ($Body | ConvertTo-Json -Depth 8)
    } catch {
        if ($_.ErrorDetails.Message) { Write-Host $_.ErrorDetails.Message }
        throw
    }
}

function Split-CommandLine {
    param([string]$Line)

    $matches = [regex]::Matches($Line, '(?:"[^"]*"|''[^'']*''|\S+)')
    $tokens = @()
    foreach ($match in $matches) {
        $token = $match.Value
        if (($token.StartsWith('"') -and $token.EndsWith('"')) -or ($token.StartsWith("'") -and $token.EndsWith("'"))) {
            $token = $token.Substring(1, $token.Length - 2)
        }
        $tokens += $token
    }
    return $tokens
}

function Save-Screenshot {
    param(
        [object]$Shot,
        [string]$Name
    )

    New-Item -ItemType Directory -Force -Path ".\artifacts\shots" | Out-Null
    $path = Join-Path ".\artifacts\shots" $Name
    [IO.File]::WriteAllBytes((Resolve-Path ".").Path + "\" + $path, [Convert]::FromBase64String($Shot.base64Image))
    return $path
}

function Find-StartedWindow {
    param(
        [object]$StartResult,
        [string]$FileName,
        [object[]]$BeforeWindows = @()
    )

    if ($StartResult.mainWindowHandle) {
        return Invoke-SlasherJson GET "/windows/$([uri]::EscapeDataString($StartResult.mainWindowHandle))"
    }

    $stem = [IO.Path]::GetFileNameWithoutExtension($FileName).ToLowerInvariant()
    $beforeHandles = @{}
    foreach ($window in $BeforeWindows) {
        $beforeHandles[$window.handle] = $true
    }

    for ($i = 0; $i -lt 28; $i++) {
        Start-Sleep -Milliseconds 250
        $windows = Invoke-SlasherJson GET "/windows"
        $candidate = $windows | ForEach-Object {
            $processName = ($_.processName -as [string])
            $lowerProcessName = if ($processName) { $processName.ToLowerInvariant() } else { "" }
            $score = 0
            if ($_.processId -eq $StartResult.processId) { $score += 120 }
            if ($lowerProcessName -eq $stem) { $score += 90 }
            elseif ($lowerProcessName -and ($lowerProcessName.Contains($stem) -or $stem.Contains($lowerProcessName))) { $score += 60 }
            if (-not $beforeHandles.ContainsKey($_.handle)) { $score += 35 }
            if ($_.isVisible) { $score += 10 }
            if (-not $_.isMinimized) { $score += 5 }
            [pscustomobject]@{ Window = $_; Score = $score }
        } | Where-Object { $_.Score -ge 60 } | Sort-Object Score -Descending | Select-Object -First 1
        if ($candidate) {
            return $candidate.Window
        }
    }

    return $null
}

try {
    Invoke-SlasherJson GET "/health" | Out-Null
} catch {
    throw "Slasher server is not running. Start it first: dotnet run --project src\Slasher\Slasher.csproj --urls $BaseUrl"
}

$selected = $null
$startedProcessId = $null
$contextShotPath = $null
$capturePath = $null

$lines = Get-Content $ScriptPath
foreach ($line in $lines) {
    $trimmed = $line.Trim()
    if (-not $trimmed -or $trimmed.StartsWith("#")) {
        continue
    }

    Write-Host "> $trimmed"
    $tokens = Split-CommandLine $trimmed
    $command = $tokens[0].ToLowerInvariant()
    $args = @($tokens | Select-Object -Skip 1)

    switch ($command) {
        "start" {
            $beforeWindows = @(Invoke-SlasherJson GET "/windows")
            $result = Invoke-SlasherJson POST "/apps/start" @{
                fileName = $args[0]
                arguments = (($args | Select-Object -Skip 1) -join " ")
            }
            $startedProcessId = $result.processId
            $selected = Find-StartedWindow -StartResult $result -FileName $args[0] -BeforeWindows $beforeWindows
            if ($selected) {
                Invoke-SlasherJson POST "/windows/$([uri]::EscapeDataString($selected.handle))/focus" @{} | Out-Null
                Write-Host "  selected $($selected.handle) $($selected.title)"
            }
        }
        "wait" {
            Start-Sleep -Milliseconds ([int]$args[0])
        }
        "move" {
            if (-not $selected) { throw "No selected window for move." }
            Invoke-SlasherJson POST "/windows/$([uri]::EscapeDataString($selected.handle))/move" @{
                x = [int]$args[0]
                y = [int]$args[1]
                width = [int]$args[2]
                height = [int]$args[3]
            } | Out-Null
        }
        "primaryclick" {
            Invoke-SlasherJson POST "/input/mouse" @{
                action = "click"
                x = [int]$args[0]
                y = [int]$args[1]
                button = "left"
            } | Out-Null
        }
        "secondaryclick" {
            Invoke-SlasherJson POST "/input/mouse" @{
                action = "click"
                x = [int]$args[0]
                y = [int]$args[1]
                button = "right"
            } | Out-Null
        }
        "text" {
            if ($selected) {
                Invoke-SlasherJson POST "/windows/$([uri]::EscapeDataString($selected.handle))/focus" @{} | Out-Null
            }
            Invoke-SlasherJson POST "/input/text" @{
                text = ($args -join " ")
            } | Out-Null
        }
        "contextmenu" {
            $result = Invoke-SlasherJson POST "/input/mouse/context-menu" @{
                x = [int]$args[0]
                y = [int]$args[1]
                delayMs = 250
            }
            $contextShotPath = Save-Screenshot $result.screenshot "mouse-demo-context-menu.bmp"
            Write-Host "  saved $contextShotPath"
        }
        "keys" {
            Invoke-SlasherJson POST "/input/keys" @{
                keys = ($args -join "+")
            } | Out-Null
        }
        "drag" {
            Invoke-SlasherJson POST "/input/mouse/drag" @{
                fromX = [int]$args[0]
                fromY = [int]$args[1]
                toX = [int]$args[2]
                toY = [int]$args[3]
                durationMs = [int]$args[4]
                button = if ($args.Count -gt 5) { $args[5] } else { "left" }
            } | Out-Null
        }
        "scroll" {
            Invoke-SlasherJson POST "/input/mouse" @{
                action = "wheel"
                wheelDelta = [int]$args[0]
            } | Out-Null
        }
        "capture" {
            if ($args[0].ToLowerInvariant() -eq "selected") {
                if (-not $selected) { throw "No selected window for capture." }
                $shot = Invoke-SlasherJson POST "/screenshot" @{ handle = $selected.handle }
            } else {
                $shot = Invoke-SlasherJson POST "/screenshot" @{}
            }
            $capturePath = Save-Screenshot $shot "mouse-demo-final.bmp"
            Write-Host "  saved $capturePath"
        }
        default {
            throw "Unsupported demo command: $command"
        }
    }
}

if (-not $KeepApp -and ($selected -or $startedProcessId)) {
    $closeProcessId = if ($selected) { $selected.processId } else { $startedProcessId }
    Invoke-SlasherJson POST "/apps/close" @{
        processId = $closeProcessId
        force = $true
    } | Out-Null
}

Write-Host "Demo completed."
if ($contextShotPath) { Write-Host "Context menu capture: $contextShotPath" }
if ($capturePath) { Write-Host "Final capture: $capturePath" }
