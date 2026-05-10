# verify-numadora-n0.ps1
#
# Numadora v0.2.1 spec 準拠検査。
#
# Phase 1 (常時実行): 静的検査
#   - scripts/numadora-host/slasher/*.numai を v0.2.1 ホスト バインディング規則で検査
#   - scripts/numadora-samples/*.numa の v0.2.1 サンプル群を構文規則で検査
#
# Phase 2 (-LiveCheck で実行): ライブ check
#   - Slasher サーバを起動し、サンプルを /scripts/check に投げて受理を確認
#   - パーサが v0.2.1 構文を受理するまで (Lang PR-B+C 完了まで) は失敗が想定される
#   - -AllowKnownFailure で 「想定済の失敗」として扱う
#
# Exit code:
#   0 - すべてのチェックが通過
#   1 - 1 件以上の致命エラー
#   2 - 警告のみ (Phase 1 で警告が出たが致命なし)

[CmdletBinding()]
param(
    [int] $Port = 5055,
    [switch] $LiveCheck,
    [switch] $AllowKnownFailure,
    [switch] $Quiet
)

$ErrorActionPreference = "Stop"

$root = Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..")
$samplesDir = Join-Path $root "scripts\numadora-samples"
$hostDir = Join-Path $root "scripts\numadora-host\slasher"

# numadora-language-spec.md 1.4.1 の能力クラス 15 種 (system-info を含む)。
$canonicalCapabilities = @(
    'observe', 'file-read', 'file-write', 'destructive',
    'user-input', 'browser-data', 'clipboard', 'process-app',
    'network-out', 'network-in', 'peer-delegate', 'secrets',
    'unattended', 'scheduling', 'system-info'
)

# 旧型名 (v0.2 でハードカット廃止)
$forbiddenTypeNames = @('Int', 'String', 'Bool', 'Float')

# v0.2.1 でリライト済のサンプル (slasher_*.numa は旧 v0.1 スタブ、PR-D/E で削除予定)
$v02Samples = @(
    'notepad-check.numa',
    'excel-showcase.numa',
    'hanoi.numa',
    'message-box.numa',
    'find-and-click.numa',
    'csv-process.numa'
)

$colorOk = 'Green'
$colorWarn = 'Yellow'
$colorErr = 'Red'
$colorInfo = 'Cyan'

function Write-Result {
    param([string] $Status, [string] $Path, [string] $Detail = '')
    if ($Quiet -and $Status -eq 'OK') { return }
    $relative = [IO.Path]::GetRelativePath($root, $Path)
    $color = switch ($Status) {
        'OK'    { $colorOk }
        'WARN'  { $colorWarn }
        'ERROR' { $colorErr }
        default { 'White' }
    }
    $line = "  [$Status] $relative"
    if ($Detail) { $line += " — $Detail" }
    Write-Host $line -ForegroundColor $color
}

function Get-FirstSignificantLine {
    param([string] $Content)
    foreach ($line in ($Content -split "`r?`n")) {
        $trim = $line.Trim()
        if ($trim -ne '' -and -not $trim.StartsWith('#') -and -not $trim.StartsWith('---')) {
            return $trim
        }
    }
    return $null
}

function Test-NumaSample {
    param([string] $Path)
    $content = Get-Content -Raw -LiteralPath $Path
    $errors = New-Object System.Collections.Generic.List[string]
    $warnings = New-Object System.Collections.Generic.List[string]

    # MODULE header
    $first = Get-FirstSignificantLine -Content $content
    if (-not $first -or -not ($first -match '^MODULE\s+\S+')) {
        $errors.Add('missing MODULE header')
    }

    # 旧 LET 区切り `:=`
    if ($content -match '(?m)^\s*(LET|VAR)\s+\S+(?:\s*:\s*[^=]+)?\s*:=') {
        $errors.Add("uses ':=' for LET/VAR; v0.2.1 requires '='")
    }

    # 旧 PascalCase 型 (case-sensitive)
    foreach ($t in $forbiddenTypeNames) {
        if ($content -cmatch "(?<![A-Za-z0-9_-])$t(?![A-Za-z0-9_-])") {
            $errors.Add("uses PascalCase type '$t'; v0.2.1 requires lowercase")
            break
        }
    }
    if ($content -cmatch 'Array<') {
        $errors.Add("uses 'Array<T>'; v0.2.1 requires 'array[T]'")
    }

    # 旧 snake モジュール
    if ($content -match '(?m)^\s*IMPORT\s+slasher_\w+') {
        $errors.Add("uses snake-case 'slasher_xxx'; v0.2.1 requires slash 'slasher/xxx'")
    }

    # IMPORT は AS 必須
    $importMatches = [regex]::Matches($content, '(?m)^\s*IMPORT\s+(\S+)(?:\s+AS\s+\w+)?')
    foreach ($m in $importMatches) {
        if ($m.Value -notmatch '\bAS\s+\w+') {
            $errors.Add("IMPORT without AS alias: $($m.Value.Trim())")
        }
    }

    # main 持ちなら REQUIRES 必須
    $hasMain = $content -match '(?m)^\s*EXPORT\s+FUNC\s+main\s*\('
    $hasReq = $content -match '(?m)^\s*REQUIRES\s*\('
    if ($hasMain -and -not $hasReq) {
        $errors.Add('module declares main but has no REQUIRES (...)')
    }
    if (-not $hasMain -and $hasReq) {
        $errors.Add('REQUIRES used in non-main module (requires_in_library)')
    }

    # REQUIRES の能力名検証
    if ($hasReq -and $content -match '(?m)^\s*REQUIRES\s*\(([^)]+)\)') {
        $caps = ($Matches[1] -split ',') | ForEach-Object { $_.Trim() } | Where-Object { $_ -ne '' }
        foreach ($cap in $caps) {
            if ($cap -notin $canonicalCapabilities) {
                $errors.Add("unknown capability '$cap' in REQUIRES")
            }
        }
    }

    return [PSCustomObject]@{
        Path = $Path
        Errors = $errors.ToArray()
        Warnings = $warnings.ToArray()
    }
}

function Test-NumaiFile {
    param([string] $Path)
    $content = Get-Content -Raw -LiteralPath $Path
    $errors = New-Object System.Collections.Generic.List[string]
    $warnings = New-Object System.Collections.Generic.List[string]

    # MODULE slasher/<name> header
    $first = Get-FirstSignificantLine -Content $content
    if (-not $first -or -not ($first -match '^MODULE\s+slasher/\S+')) {
        $errors.Add('missing MODULE slasher/<name> header')
    }

    # IMPORT は AS 必須
    $importMatches = [regex]::Matches($content, '(?m)^\s*IMPORT\s+(\S+)(?:\s+AS\s+\w+)?')
    foreach ($m in $importMatches) {
        if ($m.Value -notmatch '\bAS\s+\w+') {
            $errors.Add("IMPORT without AS alias: $($m.Value.Trim())")
        }
    }

    # EXPORT FUNC 系の検査 (能力クラス必須化)
    $funcLines = [regex]::Matches($content, '(?m)^\s*EXPORT[^\r\n]*\bFUNC\s+\S+[^\r\n]*')
    foreach ($m in $funcLines) {
        $line = $m.Value.Trim()

        # interactive_without_effect: INTERACTIVE without EFFECT
        if ($line -match 'EXPORT\s+INTERACTIVE\s+FUNC\b') {
            $errors.Add("INTERACTIVE without EFFECT: $line")
            continue
        }

        # effect_class_required: EFFECT without (class)
        if ($line -match 'EXPORT(?:\s+INTERACTIVE)?\s+EFFECT\s+FUNC\b') {
            $errors.Add("EFFECT without (class): $line")
            continue
        }

        # 能力クラスが正規 13 種か (system-info 含む = 15)
        if ($line -match 'EFFECT\(([^)]+)\)') {
            $caps = ($Matches[1] -split ',') | ForEach-Object { $_.Trim() } | Where-Object { $_ -ne '' }
            foreach ($cap in $caps) {
                if ($cap -notin $canonicalCapabilities) {
                    $errors.Add("unknown capability '$cap': $line")
                }
            }
        }
    }

    return [PSCustomObject]@{
        Path = $Path
        Errors = $errors.ToArray()
        Warnings = $warnings.ToArray()
    }
}

# Phase 1: Static spec compliance
Write-Host ""
Write-Host "Phase 1: Static v0.2.1 spec compliance" -ForegroundColor $colorInfo

$totalErrors = 0
$totalWarnings = 0

Write-Host "  -- .numai (host interfaces) --" -ForegroundColor $colorInfo
$numaiFiles = Get-ChildItem -Path $hostDir -Filter "*.numai" -ErrorAction SilentlyContinue
if (-not $numaiFiles) {
    Write-Result -Status 'WARN' -Path $hostDir -Detail 'no .numai files found'
    $totalWarnings += 1
} else {
    foreach ($f in $numaiFiles) {
        $r = Test-NumaiFile -Path $f.FullName
        $totalErrors += $r.Errors.Count
        $totalWarnings += $r.Warnings.Count
        if ($r.Errors.Count -gt 0) {
            Write-Result -Status 'ERROR' -Path $f.FullName
            foreach ($e in $r.Errors) { Write-Host "    - $e" -ForegroundColor $colorErr }
        } elseif ($r.Warnings.Count -gt 0) {
            Write-Result -Status 'WARN' -Path $f.FullName
            foreach ($w in $r.Warnings) { Write-Host "    - $w" -ForegroundColor $colorWarn }
        } else {
            Write-Result -Status 'OK' -Path $f.FullName
        }
    }
}

Write-Host "  -- .numa (v0.2.1 samples) --" -ForegroundColor $colorInfo
foreach ($name in $v02Samples) {
    $path = Join-Path $samplesDir $name
    if (-not (Test-Path -LiteralPath $path)) {
        Write-Result -Status 'ERROR' -Path $path -Detail 'expected sample missing'
        $totalErrors += 1
        continue
    }
    $r = Test-NumaSample -Path $path
    $totalErrors += $r.Errors.Count
    $totalWarnings += $r.Warnings.Count
    if ($r.Errors.Count -gt 0) {
        Write-Result -Status 'ERROR' -Path $path
        foreach ($e in $r.Errors) { Write-Host "    - $e" -ForegroundColor $colorErr }
    } elseif ($r.Warnings.Count -gt 0) {
        Write-Result -Status 'WARN' -Path $path
        foreach ($w in $r.Warnings) { Write-Host "    - $w" -ForegroundColor $colorWarn }
    } else {
        Write-Result -Status 'OK' -Path $path
    }
}

Write-Host ""
Write-Host "Phase 1 summary: errors=$totalErrors warnings=$totalWarnings" -ForegroundColor $colorInfo

if ($totalErrors -gt 0) {
    Write-Host "v0.2.1 spec compliance failed." -ForegroundColor $colorErr
    exit 1
}

# Phase 2: Live check (optional)
if (-not $LiveCheck) {
    Write-Host ""
    Write-Host "Phase 2 skipped (use -LiveCheck to run live /scripts/check against Slasher server)." -ForegroundColor $colorInfo
    if ($totalWarnings -gt 0) {
        Write-Host "OK with warnings." -ForegroundColor $colorWarn
        exit 2
    }
    Write-Host "All v0.2.1 spec checks passed." -ForegroundColor $colorOk
    exit 0
}

Write-Host ""
Write-Host "Phase 2: Live check via /scripts/check" -ForegroundColor $colorInfo

$check = Join-Path $PSScriptRoot "check-numadora.ps1"
if (-not (Test-Path -LiteralPath $check)) {
    Write-Host "check-numadora.ps1 not found at $check" -ForegroundColor $colorErr
    exit 1
}

$liveErrors = 0
foreach ($name in $v02Samples) {
    $path = Join-Path $samplesDir $name
    Write-Host "  Checking $name ..." -ForegroundColor $colorInfo
    & $check -Path $path -Port $Port
    if ($LASTEXITCODE -ne 0) {
        if ($AllowKnownFailure) {
            Write-Host "  expected failure (parser not yet v0.2.1; Lang PR-B+C pending)" -ForegroundColor $colorWarn
        } else {
            Write-Host "  FAILED. Use -AllowKnownFailure to tolerate this until parser update." -ForegroundColor $colorErr
            $liveErrors += 1
        }
    }
}

if ($liveErrors -gt 0) {
    Write-Host ""
    Write-Host "Live check failed: $liveErrors sample(s) rejected by current parser." -ForegroundColor $colorErr
    exit 1
}

Write-Host ""
Write-Host "All v0.2.1 spec + live checks passed." -ForegroundColor $colorOk
exit 0
