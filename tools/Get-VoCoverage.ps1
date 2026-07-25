<#
.SYNOPSIS
    Reports which voice lines are recorded, per language, and feeds the dashboard.

.DESCRIPTION
    Compares the keys declared in game/localization/vo.csv against the OGG files
    actually present in game/assets/audio/vo/<locale>/, and writes
    dashboard/vo-coverage.json.

    Run in CI so the dashboard always shows real recording progress rather than
    a number somebody remembered to update.

.PARAMETER Quiet
    Write the JSON without printing the per-language breakdown.
#>
[CmdletBinding()]
param([switch]$Quiet)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

$lines = Import-Csv (Join-Path $root 'game/localization/vo.csv') -Encoding UTF8
$directions = @{}
Import-Csv (Join-Path $root 'tools/vo-directions.csv') -Encoding UTF8 | ForEach-Object {
    $directions[$_.keys] = $_.speaker
}

$locales = @('en', 'fr')
$report = [ordered]@{
    generated = (Get-Date -Format 'o')
    totalLines = ($lines | Measure-Object).Count
    locales = [ordered]@{}
}

foreach ($loc in $locales) {
    $dir = Join-Path $root "game/assets/audio/vo/$loc"
    $present = @{}
    $placeholder = @{}
    if (Test-Path $dir) {
        Get-ChildItem -Path $dir -Filter '*.ogg' -File | ForEach-Object {
            $present[[System.IO.Path]::GetFileNameWithoutExtension($_.Name)] = $true
        }
        # A .placeholder marker means the OGG beside it is machine-generated TTS.
        # It plays in-game, but it is not a recorded line and must not inflate coverage.
        Get-ChildItem -Path $dir -Filter '*.placeholder' -File | ForEach-Object {
            $placeholder[[System.IO.Path]::GetFileNameWithoutExtension($_.Name)] = $true
        }
    }

    $recorded = @()
    $missing = @()
    $stubbed = @()
    foreach ($line in $lines) {
        $key = $line.keys
        if ($present.ContainsKey($key) -and -not $placeholder.ContainsKey($key)) {
            $recorded += $key
        }
        else {
            $missing += $key
            if ($placeholder.ContainsKey($key)) { $stubbed += $key }
        }
    }

    # Orphans = audio on disk with no matching key. Usually a typo in a filename.
    $declared = @{}
    $lines | ForEach-Object { $declared[$_.keys] = $true }
    $orphans = @($present.Keys | Where-Object { -not $declared.ContainsKey($_) })

    $total = ($lines | Measure-Object).Count
    $pct = if ($total -gt 0) { [math]::Round(100 * $recorded.Count / $total, 1) } else { 0 }

    $bySpeaker = [ordered]@{}
    foreach ($speaker in ($directions.Values | Sort-Object -Unique)) {
        $keys = @($lines | Where-Object { $directions[$_.keys] -eq $speaker } | ForEach-Object { $_.keys })
        $done = @($keys | Where-Object { $present.ContainsKey($_) -and -not $placeholder.ContainsKey($_) })
        $bySpeaker[$speaker] = [ordered]@{
            total    = $keys.Count
            recorded = $done.Count
        }
    }

    $report.locales[$loc] = [ordered]@{
        recorded     = $recorded.Count
        missing      = $missing.Count
        placeholders = $stubbed.Count
        percent      = $pct
        orphans      = $orphans
        bySpeaker    = $bySpeaker
        missingKeys  = $missing
    }

    if (-not $Quiet) {
        $colour = if ($pct -eq 100) { 'Green' } elseif ($pct -gt 0) { 'Yellow' } else { 'DarkGray' }
        $stub = if ($stubbed.Count -gt 0) { " [$($stubbed.Count) TTS placeholder(s)]" } else { '' }
        Write-Host ("[{0}] {1}/{2} recorded ({3}%){4}" -f $loc, $recorded.Count, $total, $pct, $stub) -ForegroundColor $colour
        if ($orphans.Count -gt 0) {
            Write-Host "  orphan files (no matching key): $($orphans -join ', ')" -ForegroundColor Red
        }
    }
}

$out = Join-Path $root 'dashboard/vo-coverage.json'
$report | ConvertTo-Json -Depth 6 | Out-File -FilePath $out -Encoding utf8
if (-not $Quiet) { Write-Host "`nWrote $out" }
