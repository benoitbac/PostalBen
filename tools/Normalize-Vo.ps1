<#
.SYNOPSIS
    Converts raw voice-over takes into game-ready localized audio.

.DESCRIPTION
    Reads WAV takes from audio-raw/<locale>/ and writes normalized OGG Vorbis into
    game/assets/audio/vo/<locale>/, applying:
      - silence trim at head and tail (keeps 120ms of room tone so cuts don't click)
      - loudness normalization to -16 LUFS / -1.5 dBTP (EBU R128, two-pass)
      - mono downmix at 48 kHz

    Every line is levelled identically, so a take recorded in June sits correctly
    next to one recorded in December without hand-riding the gain.

    Requires ffmpeg on PATH.

.PARAMETER Locale
    Which language folder to process: en, fr, or both (default).

.PARAMETER Force
    Re-encode takes whose output already exists and is newer than the source.

.EXAMPLE
    pwsh tools/Normalize-Vo.ps1 -Locale fr
#>
[CmdletBinding()]
param(
    [ValidateSet('en', 'fr', 'both')]
    [string]$Locale = 'both',
    [switch]$Force
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

if (-not (Get-Command ffmpeg -ErrorAction SilentlyContinue)) {
    throw "ffmpeg not found on PATH. Install it (winget install Gyan.FFmpeg) and reopen the shell."
}

$targetLoudness = -16
$truePeak = -1.5
$loudnessRange = 11

$locales = if ($Locale -eq 'both') { @('en', 'fr') } else { @($Locale) }
$processed = 0
$skipped = 0
$failed = @()

foreach ($loc in $locales) {
    $srcDir = Join-Path $root "audio-raw/$loc"
    $dstDir = Join-Path $root "game/assets/audio/vo/$loc"

    if (-not (Test-Path $srcDir)) {
        Write-Host "No raw takes for '$loc' ($srcDir does not exist) — skipping." -ForegroundColor DarkGray
        continue
    }

    if (-not (Test-Path $dstDir)) {
        New-Item -ItemType Directory -Path $dstDir -Force | Out-Null
    }

    $takes = Get-ChildItem -Path $srcDir -Filter '*.wav' -File
    Write-Host "`n[$loc] $($takes.Count) raw take(s)" -ForegroundColor Cyan

    foreach ($take in $takes) {
        $key = [System.IO.Path]::GetFileNameWithoutExtension($take.Name)
        $dst = Join-Path $dstDir "$key.ogg"

        if ((Test-Path $dst) -and -not $Force) {
            if ((Get-Item $dst).LastWriteTime -ge $take.LastWriteTime) {
                $skipped++
                continue
            }
        }

        # Pass 1: measure loudness so pass 2 can apply a linear (non-pumping) correction.
        $measureFilter = "loudnorm=I=$targetLoudness`:TP=$truePeak`:LRA=$loudnessRange`:print_format=json"
        $measureRaw = & ffmpeg -hide_banner -nostats -i $take.FullName -af $measureFilter -f null - 2>&1 | Out-String

        $json = [regex]::Match($measureRaw, '(?s)\{.*?\}').Value
        if (-not $json) {
            $failed += "$loc/$key (loudness measurement failed)"
            continue
        }
        $m = $json | ConvertFrom-Json

        $applyFilter = @(
            "silenceremove=start_periods=1:start_silence=0.12:start_threshold=-50dB"
            "areverse"
            "silenceremove=start_periods=1:start_silence=0.12:start_threshold=-50dB"
            "areverse"
            "loudnorm=I=$targetLoudness`:TP=$truePeak`:LRA=$loudnessRange" +
                ":measured_I=$($m.input_i):measured_TP=$($m.input_tp)" +
                ":measured_LRA=$($m.input_lra):measured_thresh=$($m.input_thresh)" +
                ":offset=$($m.target_offset):linear=true"
            "aresample=48000"
        ) -join ','

        & ffmpeg -hide_banner -loglevel error -y -i $take.FullName `
            -af $applyFilter -ac 1 -c:a libvorbis -q:a 5 $dst

        if ($LASTEXITCODE -ne 0) {
            $failed += "$loc/$key (encode failed)"
            continue
        }

        $processed++
        Write-Host "  ok  $key" -ForegroundColor Green
    }
}

Write-Host "`nProcessed $processed, skipped $skipped (already current)." -ForegroundColor Cyan
if ($failed.Count -gt 0) {
    Write-Host "Failed:" -ForegroundColor Red
    $failed | ForEach-Object { Write-Host "  $_" -ForegroundColor Red }
    exit 1
}
