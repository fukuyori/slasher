param(
    [Parameter(Mandatory = $true)]
    [string] $Path,

    [string] $NumadoraHome = $env:NUMADORA_HOME,

    [string] $TargetDir = ""
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($NumadoraHome)) {
    $candidate = "D:\home\source\rust\Numadora"
    if (Test-Path -LiteralPath $candidate) {
        $NumadoraHome = $candidate
    }
}

if ([string]::IsNullOrWhiteSpace($NumadoraHome) -or -not (Test-Path -LiteralPath $NumadoraHome)) {
    throw "Numadora home was not found. Set NUMADORA_HOME or pass -NumadoraHome."
}

$manifest = Join-Path $NumadoraHome "Cargo.toml"
if (-not (Test-Path -LiteralPath $manifest)) {
    throw "Numadora home does not contain Cargo.toml: $NumadoraHome"
}

$resolvedPath = Resolve-Path -LiteralPath $Path
$scriptRoot = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($TargetDir)) {
    $TargetDir = Join-Path (Join-Path $scriptRoot ".numadora-targets") "default"
}
elseif (-not [System.IO.Path]::IsPathRooted($TargetDir)) {
    $TargetDir = Join-Path (Get-Location) $TargetDir
}
Push-Location $NumadoraHome
$previousCargoTargetDir = $env:CARGO_TARGET_DIR
try {
    New-Item -ItemType Directory -Force -Path $TargetDir | Out-Null
    $env:CARGO_TARGET_DIR = $TargetDir
    cargo run -- check $resolvedPath
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
}
finally {
    $env:CARGO_TARGET_DIR = $previousCargoTargetDir
    Pop-Location
}
