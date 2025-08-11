#!/usr/bin/env pwsh
$ErrorActionPreference = "Stop"

$headers = @{
    "Accept" = "application/vnd.github+json"
    "X-GitHub-Api-Version" = "2022-11-28"
}

$release = Invoke-RestMethod -Uri "https://api.github.com/repos/K-Wojciechowski/CleanupForNotion/releases/latest" -Headers $headers
$asset = $release.assets | Where-Object { $_.name -eq "lambda.zip" } | Select-Object -First 1

Write-Host "Latest release: $($release.tag_name)"

if ($null -eq $asset) {
  Write-Error "No assets found in the latest release."
  Exit 1
}

$zipUrl = $asset.browser_download_url
$targetPath = Join-Path $PSScriptRoot "lambda.zip"
Write-Host "Downloading $zipUrl to $targetPath"
Invoke-WebRequest -Uri $zipUrl -OutFile $targetPath
Get-Item $targetPath
