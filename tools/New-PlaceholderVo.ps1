<#
.SYNOPSIS
    Generates throwaway TTS voice lines so gameplay can be built before Ben records.

.DESCRIPTION
    Speaks every line in game/localization/vo.csv using a Windows SAPI voice and
    writes it into game/assets/audio/vo/<locale>/ as OGG, exactly where the real
    take will eventually go.

    These are PLACEHOLDERS. They are marked as such in dashboard/vo-coverage.json
    via a sidecar .placeholder marker file, so the dashboard never counts a robot
    voice as a recorded line. Normalize-Vo.ps1 overwrites them the moment a real
    take lands on the same key.

.PARAMETER Locale
    en, fr, or both (default).

.PARAMETER Clean
    Delete all existing placeholders instead of generating.

.EXAMPLE
    pwsh tools/New-PlaceholderVo.ps1 -Locale en
#>
[CmdletBinding()]
param(
    [ValidateSet('en', 'fr', 'both')]
    [string]$Locale = 'both',
    [switch]$Clean
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$locales = if ($Locale -eq 'both') { @('en', 'fr') } else { @($Locale) }

if ($Clean) {
    foreach ($loc in $locales) {
        $dir = Join-Path $root "game/assets/audio/vo/$loc"
        if (-not (Test-Path $dir)) { continue }
        $markers = Get-ChildItem -Path $dir -Filter '*.placeholder' -File -ErrorAction SilentlyContinue
        foreach ($marker in $markers) {
            $key = [System.IO.Path]::GetFileNameWithoutExtension($marker.Name)
            Remove-Item (Join-Path $dir "$key.ogg") -ErrorAction SilentlyContinue
            Remove-Item $marker.FullName
        }
        Write-Host "[$loc] removed $($markers.Count) placeholder(s)"
    }
    exit 0
}

Add-Type -AssemblyName System.Speech

if (-not (Get-Command ffmpeg -ErrorAction SilentlyContinue)) {
    throw "ffmpeg not found on PATH. Install it (winget install Gyan.FFmpeg) and reopen the shell."
}

$lines = Import-Csv (Join-Path $root 'game/localization/vo.csv') -Encoding UTF8
$synth = [System.Speech.Synthesis.SpeechSynthesizer]::new()
$installed = $synth.GetInstalledVoices() | Where-Object { $_.Enabled } | ForEach-Object { $_.VoiceInfo }

foreach ($loc in $locales) {
    # SAPI culture names are like en-US / fr-FR; match on the language part only.
    $voice = $installed | Where-Object { $_.Culture.TwoLetterISOLanguageName -eq $loc } | Select-Object -First 1

    if (-not $voice) {
        Write-Host "[$loc] no SAPI voice installed for this language - skipping." -ForegroundColor Yellow
        Write-Host "      Add one via Settings > Time & Language > Speech, then re-run." -ForegroundColor DarkGray
        continue
    }

    $synth.SelectVoice($voice.Name)
    $synth.Rate = -1

    $dir = Join-Path $root "game/assets/audio/vo/$loc"
    if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }

    $made = 0
    $kept = 0

    foreach ($line in $lines) {
        $key = $line.keys
        $ogg = Join-Path $dir "$key.ogg"
        $marker = Join-Path $dir "$key.placeholder"

        # Never clobber a real take: an OGG with no marker beside it is Ben's voice.
        if ((Test-Path $ogg) -and -not (Test-Path $marker)) {
            $kept++
            continue
        }

        $wav = Join-Path $env:TEMP "postalben-tts-$key.wav"
        $synth.SetOutputToWaveFile($wav)
        $synth.Speak($line.$loc)
        $synth.SetOutputToNull()

        & ffmpeg -hide_banner -loglevel error -y -i $wav -ac 1 -ar 48000 -c:a libvorbis -q:a 3 $ogg
        Remove-Item $wav -ErrorAction SilentlyContinue

        if ($LASTEXITCODE -eq 0) {
            "placeholder TTS - replace with a real take" | Out-File -FilePath $marker -Encoding utf8
            $made++
        }
    }

    Write-Host "[$loc] $made placeholder(s) generated using '$($voice.Name)', $kept real take(s) left alone." -ForegroundColor Cyan
}

$synth.Dispose()
Write-Host "`nPlaceholders are excluded from recording coverage. Run tools/Get-VoCoverage.ps1 to confirm."
