param(
    [string] $NumadoraHome = $env:NUMADORA_HOME
)

$ErrorActionPreference = "Stop"

$root = Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..")
$check = Join-Path $PSScriptRoot "check-numadora.ps1"
$probeId = [Guid]::NewGuid().ToString("N")
$targetRoot = Join-Path $root ".numadora-targets"

if ([string]::IsNullOrWhiteSpace($NumadoraHome)) {
    $candidate = "D:\home\source\rust\Numadora"
    if (Test-Path -LiteralPath $candidate) {
        $NumadoraHome = $candidate
    }
}

if ([string]::IsNullOrWhiteSpace($NumadoraHome) -or -not (Test-Path -LiteralPath $NumadoraHome)) {
    throw "Numadora home was not found. Set NUMADORA_HOME or pass -NumadoraHome."
}

$baselineSample = Join-Path $NumadoraHome "examples\module.numa"
if (-not (Test-Path -LiteralPath $baselineSample)) {
    throw "Numadora baseline sample was not found: $baselineSample"
}

Write-Host "N0 probe: checking Numadora baseline module sample"
& $check `
    -Path $baselineSample `
    -NumadoraHome $NumadoraHome `
    -TargetDir (Join-Path $targetRoot "n0-baseline-$probeId")
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

Write-Host "N0 probe: checking Slasher target sample"
& $check `
    -Path (Join-Path $root "scripts\numadora-samples\notepad-check.numa") `
    -NumadoraHome $NumadoraHome `
    -TargetDir (Join-Path $targetRoot "n0-slasher-$probeId")
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

Write-Host "N0 probe passed: baseline and Slasher current-spec sample both check successfully."
