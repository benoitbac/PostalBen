<#
.SYNOPSIS
    Downloads the CC0 texture set the district is built from.

.DESCRIPTION
    Pulls 1k JPG maps (diffuse, normal, roughness) from Poly Haven for each material
    named in the manifest below, into game/assets/textures/<name>/.

    Everything here is CC0 - usable in a public repo with no attribution requirement,
    though CREDITS.md names the authors anyway. 1k is deliberate: it is enough for a
    blockout being viewed from street level, and keeps the repo in the tens of MB
    rather than the hundreds.

    Idempotent - files already present are skipped unless -Force.

.PARAMETER Force
    Re-download files that already exist.

.EXAMPLE
    pwsh tools/Fetch-Assets.ps1
#>
[CmdletBinding()]
param([switch]$Force)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$outRoot = Join-Path $root 'game/assets/textures'

# Poly Haven asset id -> the role it plays in the district.
$manifest = [ordered]@{
    # Arid suburban palette - sun-bleached stucco, dust and concrete, not European brick.
    'dry_ground_rocks'        = 'ground: dry dirt and scrub'
    'sand_02'                 = 'ground: sand verges and lots'
    'painted_plaster_wall'    = 'stucco walls (the default here)'
    'concrete_layers_02'      = 'commercial and civic blocks'
    'corrugated_iron_02'      = 'warehouses, sheds, roller shutters'
    'red_brick_03'            = 'accent walls, older buildings'
    'asphalt_04'              = 'road surface'
    'concrete_floor_worn_001' = 'kerbs and sidewalks'
    'concrete_pavers_02'      = 'forecourts and parking lots'
    'wood_planks_grey'        = 'doors, porches, shop fittings'
    'brick_wall_006'          = 'legacy accent (kept for variety)'
    'asphalt_02'              = 'legacy road (kept for variety)'
    'pavement_02'             = 'legacy paving (kept for variety)'
    'grass_medium_01'         = 'the one patch of watered lawn'
}

# Poly Haven's file tree keys these differently per map type.
$maps = @{
    'diff'   = @('Diffuse')
    'nor_gl' = @('nor_gl')
    'rough'  = @('Rough')
}

$downloaded = 0
$skipped = 0

foreach ($asset in $manifest.Keys) {
    $dir = Join-Path $outRoot $asset
    if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }

    Write-Host "[$asset] $($manifest[$asset])" -ForegroundColor Cyan

    try {
        $files = Invoke-RestMethod -Uri "https://api.polyhaven.com/files/$asset" -TimeoutSec 30
    }
    catch {
        Write-Host "  FAILED to query: $($_.Exception.Message)" -ForegroundColor Red
        continue
    }

    foreach ($suffix in $maps.Keys) {
        $key = $maps[$suffix][0]
        $node = $files.$key

        if (-not $node) {
            Write-Host "  no '$key' map" -ForegroundColor DarkGray
            continue
        }

        $url = $node.'1k'.jpg.url
        if (-not $url) {
            Write-Host "  no 1k jpg for '$key'" -ForegroundColor DarkGray
            continue
        }

        $dest = Join-Path $dir "$asset`_$suffix`_1k.jpg"
        if ((Test-Path $dest) -and -not $Force) {
            $skipped++
            continue
        }

        try {
            Invoke-WebRequest -Uri $url -OutFile $dest -TimeoutSec 120
            $size = [math]::Round((Get-Item $dest).Length / 1KB)
            Write-Host "  ok  $suffix (${size} KB)" -ForegroundColor Green
            $downloaded++
        }
        catch {
            Write-Host "  FAILED $suffix : $($_.Exception.Message)" -ForegroundColor Red
        }
    }
}

$total = (Get-ChildItem $outRoot -Recurse -Filter *.jpg -ErrorAction SilentlyContinue |
    Measure-Object Length -Sum).Sum / 1MB

Write-Host ""
Write-Host "Downloaded $downloaded, skipped $skipped (already present)." -ForegroundColor Cyan
Write-Host ("Texture set on disk: {0:N1} MB" -f $total) -ForegroundColor Cyan
