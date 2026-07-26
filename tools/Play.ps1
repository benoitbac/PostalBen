<#
.SYNOPSIS
    Builds and launches PostalBen.

.DESCRIPTION
    Finds the Godot .NET binary, compiles the C# assembly, imports assets if needed,
    then launches the game. Handles the two failure modes that bite most often:
    a missing Godot install, and running the standard (non-.NET) Godot build, which
    cannot execute C# and fails with confusing autoload errors.

.PARAMETER Locale
    Force a language for this run: en or fr. Omit to use the saved/system setting.

.PARAMETER Tests
    Run the headless invariant suite instead of the game.

.PARAMETER Editor
    Open the Godot editor on the project instead of playing.

.EXAMPLE
    pwsh tools/Play.ps1
    pwsh tools/Play.ps1 -Locale fr
    pwsh tools/Play.ps1 -Tests
#>
[CmdletBinding()]
param(
    [ValidateSet('en', 'fr')]
    [string]$Locale,
    [switch]$Tests,
    [switch]$Editor
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$gameDir = Join-Path $root 'game'

function Find-Godot {
    $candidates = @()

    if ($env:GODOT) { $candidates += $env:GODOT }

    $cmd = Get-Command godot -ErrorAction SilentlyContinue
    if ($cmd) { $candidates += $cmd.Source }

    # Common manual-install locations, newest-looking first.
    $candidates += Get-ChildItem -Path 'C:\Tools\godot', "$env:LOCALAPPDATA\Godot", 'C:\Program Files\Godot' `
        -Filter '*mono*console.exe' -Recurse -ErrorAction SilentlyContinue |
        Sort-Object Name -Descending | Select-Object -ExpandProperty FullName

    $candidates += Get-ChildItem -Path 'C:\Tools\godot', "$env:LOCALAPPDATA\Godot", 'C:\Program Files\Godot' `
        -Filter '*mono*.exe' -Recurse -ErrorAction SilentlyContinue |
        Sort-Object Name -Descending | Select-Object -ExpandProperty FullName

    foreach ($c in $candidates) {
        if ($c -and (Test-Path $c)) { return $c }
    }
    return $null
}

$godot = Find-Godot
if (-not $godot) {
    Write-Host "Godot not found." -ForegroundColor Red
    Write-Host ""
    Write-Host "Download the .NET build (NOT the standard one) from:" -ForegroundColor Yellow
    Write-Host "  https://godotengine.org/download/windows/"
    Write-Host "Extract it anywhere, then either add it to PATH or set:" -ForegroundColor Yellow
    Write-Host '  $env:GODOT = "C:\path\to\Godot_v4.7.1-stable_mono_win64.exe"'
    exit 1
}

if ($godot -notmatch 'mono') {
    Write-Host "Warning: '$godot' does not look like the .NET/Mono build." -ForegroundColor Yellow
    Write-Host "PostalBen is written in C# and will not run on the standard build." -ForegroundColor Yellow
}

Write-Host "Godot: $godot" -ForegroundColor DarkGray

Push-Location $gameDir
try {
    Write-Host "Building C#..." -ForegroundColor Cyan
    dotnet build PostalBen.csproj --nologo -v q
    if ($LASTEXITCODE -ne 0) { throw "C# build failed." }

    if (-not (Test-Path (Join-Path $gameDir '.godot'))) {
        Write-Host "First run - importing assets (this takes a moment)..." -ForegroundColor Cyan
        & $godot --headless --import --quit-after 600 | Out-Null
    }

    if ($Editor) {
        Write-Host "Opening editor..." -ForegroundColor Cyan
        & $godot --editor
        return
    }

    $godotArgs = @()
    if ($Locale) { $godotArgs += @('--language', $Locale) }

    if ($Tests) {
        Write-Host "Running invariant tests..." -ForegroundColor Cyan
        & $godot --headless @godotArgs -- --run-tests
        exit $LASTEXITCODE
    }

    Write-Host ""
    Write-Host "Controls: WASD move, mouse look, Shift sprint, C crouch," -ForegroundColor Green
    Write-Host "          E interact, J errand list, Esc release mouse." -ForegroundColor Green
    Write-Host ""
    & $godot @godotArgs
}
finally {
    Pop-Location
}
