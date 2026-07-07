#requires -Version 5.1
<#
    Generate (or update) the repository manifest.json that Jellyfin's
    "Repositories" feature reads.

    IMPORTANT: this is a different file from meta.json.
      - meta.json   = the "name tag" INSIDE the plugin zip (one version only).
      - manifest.json = the "catalog" at the REPO ROOT, listing ALL versions,
                        each pointing to its own zip on GitHub Releases.

    This script assumes you already:
      1) ran .\package.ps1 (creates dist\CFilm_<version>.zip)
      2) uploaded that zip as an asset on a GitHub Release tagged <version>

    It then computes the MD5 checksum of the zip and writes/updates
    manifest.json at the repo root with a new version entry.

    NOTE: ASCII-only script (see package.ps1 for why: PowerShell 5.1 on a
    Japanese OS misreads BOM-less UTF-8 as Shift-JIS and corrupts non-ASCII text).

    Usage:
      .\generate-manifest.ps1 -Version 1.0.0.0 -RepoOwner Chige-c -RepoName jellyfin-plugin-cfilm
#>
[CmdletBinding()]
param(
    [string]$Version   = "1.0.0.0",
    [string]$TargetAbi = "10.11.11.0",
    [Parameter(Mandatory = $true)][string]$RepoOwner,
    [Parameter(Mandatory = $true)][string]$RepoName,
    [string]$Changelog = "Initial release."
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$zip  = Join-Path $root "dist\CFilm_$Version.zip"

if (-not (Test-Path $zip)) {
    throw "Zip not found: $zip . Run .\package.ps1 first."
}

Write-Host "== Computing MD5 checksum ==" -ForegroundColor Cyan
$md5 = (Get-FileHash -Path $zip -Algorithm MD5).Hash.ToLower()
Write-Host "checksum: $md5"

$sourceUrl = "https://github.com/$RepoOwner/$RepoName/releases/download/v$Version/CFilm_$Version.zip"
$timestamp = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss")

$newVersionEntry = [ordered]@{
    version    = $Version
    changelog  = $Changelog
    targetAbi  = $TargetAbi
    sourceUrl  = $sourceUrl
    checksum   = $md5
    timestamp  = $timestamp
}

$manifestPath = Join-Path $root "manifest.json"

if (Test-Path $manifestPath) {
    Write-Host "== Updating existing manifest.json ==" -ForegroundColor Cyan
    $manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json
} else {
    Write-Host "== Creating new manifest.json ==" -ForegroundColor Cyan
    $manifest = @()
}

# manifest.json is a JSON ARRAY of plugin entries (one entry per plugin,
# each with a "versions" array holding every release). We have one plugin.
$existing = $manifest | Where-Object { $_.guid -eq "596aa080-416e-46f0-805b-6d499f1cabd8" } | Select-Object -First 1

if ($null -eq $existing) {
    $entry = [ordered]@{
        guid        = "596aa080-416e-46f0-805b-6d499f1cabd8"
        name        = "CFilm"
        description = "C-film custom features for Jellyfin: an ordered Recommendations row and VOD (streaming service) identification via TMDB watch providers (JP / flatrate)."
        overview    = "Recommendations row + VOD identification"
        owner       = $RepoOwner
        category    = "General"
        versions    = @($newVersionEntry)
    }
    $manifest = @($entry)
} else {
    # Remove any existing entry with the same version (re-publish), then prepend the new one.
    $otherVersions = @($existing.versions | Where-Object { $_.version -ne $Version })
    $existing.versions = @($newVersionEntry) + $otherVersions

    # Rebuild the manifest array with the updated entry in place.
    $manifest = @($manifest | ForEach-Object {
        if ($_.guid -eq "596aa080-416e-46f0-805b-6d499f1cabd8") { $existing } else { $_ }
    })
}

$json = $manifest | ConvertTo-Json -Depth 10
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText($manifestPath, $json, $utf8NoBom)

Write-Host ""
Write-Host "== Done ==" -ForegroundColor Green
Write-Host "manifest.json written: $manifestPath"
Write-Host "sourceUrl for this version: $sourceUrl"
Write-Host ""
Write-Host "Next: commit and push manifest.json, then in Jellyfin add this repository URL:"
Write-Host "  https://raw.githubusercontent.com/$RepoOwner/$RepoName/main/manifest.json"
