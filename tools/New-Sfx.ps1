<#
.SYNOPSIS
    Synthesises the sound effect set from scratch.

.DESCRIPTION
    Writes 16-bit mono PCM WAVs into game/assets/audio/sfx/.

    Everything here is generated from noise and sine oscillators rather than sampled,
    so the whole set is original work with no licence attached and no download step -
    `git clone` gives you a project that already makes noise.

    Deterministic: a fixed seed means the same build produces byte-identical files, so
    regenerating never shows up as a spurious diff.

    These are placeholders in the same sense as the grey-box district: correct in
    function, obviously synthetic, and meant to be replaced by recorded foley.

.PARAMETER Force
    Overwrite files that already exist.
#>
[CmdletBinding()]
param([switch]$Force)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$outDir = Join-Path $root 'game/assets/audio/sfx'
if (-not (Test-Path $outDir)) { New-Item -ItemType Directory -Path $outDir -Force | Out-Null }

$rate = 22050

# Deterministic LCG. Implemented in C# rather than PowerShell because the 64-bit
# multiply has to wrap silently, and PowerShell raises on unsigned overflow instead.
# System.Random is not an option either: its algorithm is not guaranteed stable
# across runtimes, so the same script could emit different audio on another machine.
Add-Type -TypeDefinition @'
public static class Lcg {
    private static ulong _s = 88172645463325252UL;
    public static void Reset() { _s = 88172645463325252UL; }
    public static double Next() {
        unchecked { _s = _s * 6364136223846793005UL + 1442695040888963407UL; }
        return (((_s >> 33) & 0xFFFFFF) / 16777215.0) * 2.0 - 1.0;
    }
}
'@ -ErrorAction SilentlyContinue

[Lcg]::Reset()
function Rand { return [Lcg]::Next() }

function Write-Wav {
    param([string]$Name, [double[]]$Samples)

    $path = Join-Path $outDir "$Name.wav"
    if ((Test-Path $path) -and -not $Force) {
        Write-Host "  skip $Name (exists)" -ForegroundColor DarkGray
        return
    }

    $count = $Samples.Length
    $stream = [System.IO.File]::Create($path)
    $w = New-Object System.IO.BinaryWriter($stream)

    $dataBytes = $count * 2
    $w.Write([char[]]'RIFF');           $w.Write([int](36 + $dataBytes))
    $w.Write([char[]]'WAVE')
    $w.Write([char[]]'fmt ');           $w.Write([int]16)
    $w.Write([int16]1);                 $w.Write([int16]1)      # PCM, mono
    $w.Write([int]$rate);               $w.Write([int]($rate * 2))
    $w.Write([int16]2);                 $w.Write([int16]16)
    $w.Write([char[]]'data');           $w.Write([int]$dataBytes)

    foreach ($s in $Samples) {
        $v = [math]::Max(-1.0, [math]::Min(1.0, $s))
        $w.Write([int16]([math]::Round($v * 32000)))
    }

    $w.Close(); $stream.Close()
    $kb = [math]::Round((Get-Item $path).Length / 1KB)
    Write-Host "  ok   $Name (${kb} KB)" -ForegroundColor Green
}

# One-pole low-pass. Cheap, and enough to turn white noise into something with a body.
function LowPass {
    param([double[]]$In, [double]$Cutoff)
    $a = [math]::Exp(-2.0 * [math]::PI * $Cutoff / $rate)
    $out = New-Object 'double[]' $In.Length
    $prev = 0.0
    for ($i = 0; $i -lt $In.Length; $i++) {
        $prev = $In[$i] * (1.0 - $a) + $prev * $a
        $out[$i] = $prev
    }
    return $out
}

function Noise { param([int]$N) $a = New-Object 'double[]' $N; for ($i = 0; $i -lt $N; $i++) { $a[$i] = Rand }; return $a }

function Envelope {
    param([double[]]$In, [double]$AttackMs, [double]$DecayShape)
    $atk = [int]($AttackMs * $rate / 1000)
    $out = New-Object 'double[]' $In.Length
    for ($i = 0; $i -lt $In.Length; $i++) {
        $t = $i / [double]$In.Length
        $a = if ($i -lt $atk -and $atk -gt 0) { $i / [double]$atk } else { 1.0 }
        $out[$i] = $In[$i] * $a * [math]::Pow(1.0 - $t, $DecayShape)
    }
    return $out
}

function Tone {
    param([int]$N, [double]$Hz, [double]$Drop = 0.0)
    $out = New-Object 'double[]' $N
    $phase = 0.0
    for ($i = 0; $i -lt $N; $i++) {
        $f = $Hz * (1.0 - $Drop * ($i / [double]$N))
        $phase += 2.0 * [math]::PI * $f / $rate
        $out[$i] = [math]::Sin($phase)
    }
    return $out
}

function Mix { param([double[]]$A, [double[]]$B, [double]$GainA, [double]$GainB)
    $n = [math]::Max($A.Length, $B.Length)
    $out = New-Object 'double[]' $n
    for ($i = 0; $i -lt $n; $i++) {
        $va = if ($i -lt $A.Length) { $A[$i] } else { 0.0 }
        $vb = if ($i -lt $B.Length) { $B[$i] } else { 0.0 }
        $out[$i] = $va * $GainA + $vb * $GainB
    }
    return $out
}

Write-Host "Synthesising SFX at ${rate} Hz..." -ForegroundColor Cyan

# --- Footsteps: a short scuff. Four variants so a walk cycle does not machine-gun.
for ($v = 1; $v -le 4; $v++) {
    $n = [int]($rate * 0.13)
    $body = LowPass (Noise $n) (900 + $v * 220)
    Write-Wav "step_$v" (Envelope $body 2 4.5)
}

# --- Gunshot: cracking noise over a low thump, which is what gives it weight.
$n = [int]($rate * 0.45)
$crack = Envelope (LowPass (Noise $n) 5200) 0.4 7
$thump = Envelope (Tone $n 90 0.6) 1 5
Write-Wav 'gunshot' (Mix $crack $thump 0.85 0.5)

# --- Melee swing: filtered noise that swells and fades. No impact in it.
$n = [int]($rate * 0.3)
$sw = LowPass (Noise $n) 1400
$out = New-Object 'double[]' $n
for ($i = 0; $i -lt $n; $i++) {
    $t = $i / [double]$n
    $out[$i] = $sw[$i] * [math]::Sin([math]::PI * $t) * 0.7
}
Write-Wav 'swing' $out

# --- Melee impact: dull, low, unpleasant. Deliberately not a satisfying crunch.
$n = [int]($rate * 0.26)
$hit = Mix (Envelope (LowPass (Noise $n) 700) 0.5 5) (Envelope (Tone $n 120 0.5) 0.5 4) 0.6 0.7
Write-Wav 'impact' $hit

# --- Body hitting the ground.
$n = [int]($rate * 0.5)
$fall = Mix (Envelope (LowPass (Noise $n) 420) 1 4) (Envelope (Tone $n 65 0.4) 2 3.2) 0.55 0.8
Write-Wav 'bodyfall' $fall

# --- Interaction blip: two-tone, quiet, unobtrusive.
$n = [int]($rate * 0.11)
Write-Wav 'interact' (Envelope (Tone $n 880 -0.35) 1 3.5)

# --- Till: a register chirp for a completed transaction.
$n = [int]($rate * 0.22)
Write-Wav 'till' (Mix (Envelope (Tone $n 1320 0.0) 1 4) (Envelope (Tone $n 1760 0.0) 30 4) 0.5 0.4)

# --- Ambience: a long, quiet, seamless bed of wind and distant traffic.
$n = [int]($rate * 8)
$amb = LowPass (Noise $n) 220
$out = New-Object 'double[]' $n
$fade = [int]($rate * 0.5)
for ($i = 0; $i -lt $n; $i++) {
    # Slow swell so it does not sit as flat hiss.
    $swell = 0.6 + 0.4 * [math]::Sin(2.0 * [math]::PI * $i / ($rate * 5.3))
    $g = 1.0
    if ($i -lt $fade) { $g = $i / [double]$fade }
    elseif ($i -gt $n - $fade) { $g = ($n - $i) / [double]$fade }
    $out[$i] = $amb[$i] * $swell * $g * 0.5
}
Write-Wav 'ambience' $out

Write-Host "`nDone. Set them to Loop in the import panel only for 'ambience'." -ForegroundColor Cyan
