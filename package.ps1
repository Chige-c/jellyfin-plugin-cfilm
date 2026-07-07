#requires -Version 5.1
<#
    Package the CFilm plugin into an installable folder + zip.
    Steps:
      1) Build (Release)
      2) Put the DLL and meta.json into dist\CFilm_<version>\
      3) Zip it (easy to transfer to the server)

    NOTE: This script is intentionally ASCII-only. Windows PowerShell 5.1
    reads a BOM-less .ps1 using the system ANSI code page (Shift-JIS on a
    Japanese OS), which corrupts UTF-8 non-ASCII text and breaks parsing.

    Usage (PowerShell):
      .\package.ps1
      .\package.ps1 -Version 1.0.1.0
#>
[CmdletBinding()]
param(
    [string]$Version   = "1.0.0.0",
    [string]$TargetAbi = "10.11.11.0"
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$proj = Join-Path $root "Jellyfin.Plugin.CFilm\Jellyfin.Plugin.CFilm.csproj"

Write-Host "== Build (Release) ==" -ForegroundColor Cyan
dotnet build $proj -c Release
if ($LASTEXITCODE -ne 0) { throw "Build failed" }

$dll = Join-Path $root "Jellyfin.Plugin.CFilm\bin\Release\net9.0\Jellyfin.Plugin.CFilm.dll"
if (-not (Test-Path $dll)) { throw "DLL not found: $dll" }

$folderName = "CFilm_$Version"
$distRoot   = Join-Path $root "dist"
$distDir    = Join-Path $distRoot $folderName
if (Test-Path $distDir) { Remove-Item $distDir -Recurse -Force }
New-Item -ItemType Directory -Force -Path $distDir | Out-Null

Copy-Item $dll $distDir -Force

# meta.json: the "name tag" Jellyfin reads to recognize the plugin.
# Keys must match Jellyfin's PluginManifest exactly (all camelCase).
$meta = [ordered]@{
    category    = "General"
    changelog   = "$Version - Initial release."
    description = "C-film custom features for Jellyfin: an ordered Recommendations row and VOD (streaming service) identification via TMDB watch providers (JP / flatrate)."
    guid        = "596aa080-416e-46f0-805b-6d499f1cabd8"
    name        = "CFilm"
    overview    = "Recommendations row + VOD identification"
    owner       = "chida"
    targetAbi   = $TargetAbi
    timestamp   = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
    version     = $Version
    status      = "Active"
    autoUpdate  = $true
    imagePath   = $null
    assemblies  = @("Jellyfin.Plugin.CFilm.dll")
}
$json = $meta | ConvertTo-Json
# Write UTF-8 without BOM (a BOM can break Jellyfin's JSON reader).
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText((Join-Path $distDir "meta.json"), $json, $utf8NoBom)

$zip = Join-Path $distRoot "$folderName.zip"
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path (Join-Path $distDir "*") -DestinationPath $zip -Force

Write-Host ""
Write-Host "== Done ==" -ForegroundColor Green
Write-Host "Install folder: $distDir"
Write-Host "Zip          : $zip"
Get-ChildItem $distDir | Select-Object Name, Length | Format-Table -AutoSize
