<#
.SYNOPSIS
    Applies .github/labels.yml to the GitHub repository.

.DESCRIPTION
    Creates or updates every label defined in .github/labels.yml via the gh CLI.
    Idempotent - safe to re-run after editing the file.

    Uses a minimal parser rather than a YAML module so the script has no dependency
    beyond gh itself. The file format is deliberately flat: repeated blocks of
    name/color/description.

.PARAMETER Repo
    Target repository as owner/name. Defaults to the current directory's remote.

.PARAMETER Prune
    Also delete labels present on the repo but absent from labels.yml. This removes
    GitHub's stock labels; review before using.

.EXAMPLE
    pwsh tools/Sync-Labels.ps1 -Repo benoitbac/PostalBen
#>
[CmdletBinding()]
param(
    [string]$Repo,
    [switch]$Prune
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
    throw "gh CLI not found. Install it from https://cli.github.com and run 'gh auth login'."
}

$repoArgs = if ($Repo) { @('--repo', $Repo) } else { @() }

$labels = @()
$current = $null

foreach ($line in Get-Content (Join-Path $root '.github/labels.yml') -Encoding UTF8) {
    $trimmed = $line.Trim()
    if ($trimmed -eq '' -or $trimmed.StartsWith('#')) { continue }

    if ($trimmed -match '^-\s*name:\s*"?(.+?)"?$') {
        if ($current) { $labels += $current }
        $current = [ordered]@{ name = $Matches[1]; color = 'ededed'; description = '' }
    }
    elseif ($trimmed -match '^color:\s*"?(.+?)"?$' -and $current) {
        $current.color = $Matches[1]
    }
    elseif ($trimmed -match '^description:\s*"?(.+?)"?$' -and $current) {
        $current.description = $Matches[1]
    }
}
if ($current) { $labels += $current }

Write-Host "Syncing $($labels.Count) label(s)..." -ForegroundColor Cyan

$failed = @()
foreach ($label in $labels) {
    # --force turns create into upsert, so colour and description edits apply too.
    gh label create $label.name --color $label.color --description $label.description --force @repoArgs 2>&1 | Out-Null
    if ($LASTEXITCODE -eq 0) {
        Write-Host "  ok  $($label.name)" -ForegroundColor Green
    }
    else {
        $failed += $label.name
        Write-Host "  FAIL $($label.name)" -ForegroundColor Red
    }
}

if ($Prune) {
    $defined = $labels.name
    $existing = gh label list @repoArgs --limit 200 --json name | ConvertFrom-Json
    foreach ($label in $existing) {
        if ($defined -notcontains $label.name) {
            Write-Host "  del $($label.name)" -ForegroundColor DarkYellow
            gh label delete $label.name --yes @repoArgs 2>&1 | Out-Null
        }
    }
}

if ($failed.Count -gt 0) {
    Write-Host "`n$($failed.Count) label(s) failed: $($failed -join ', ')" -ForegroundColor Red
    exit 1
}
Write-Host "`nDone." -ForegroundColor Cyan
