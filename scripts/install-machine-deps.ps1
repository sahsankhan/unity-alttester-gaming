# Installs what CAN be automated on Windows. Unity + TrashCat build still need Unity Editor (see App/README.md).
$ErrorActionPreference = "Continue"

Write-Host "Installing .NET 8 SDK (skip if already installed)..."
winget install Microsoft.DotNet.SDK.8 --accept-package-agreements --accept-source-agreements --disable-interactivity 2>$null

Write-Host "Installing Unity Hub..."
winget install Unity.UnityHub --accept-package-agreements --accept-source-agreements --disable-interactivity

Write-Host "Installing ffmpeg (optional, for MP4 reports)..."
winget install Gyan.FFmpeg --accept-package-agreements --accept-source-agreements --disable-interactivity 2>$null

Write-Host ""
Write-Host "Next (you only, in Unity — cannot be scripted):"
Write-Host "  1. Unity Hub -> Install 2022.3 LTS + Windows Build Support"
Write-Host "  2. Import Endless Runner sample + AltTester SDK from https://alttester.com/downloads/"
Write-Host "  3. Build to: App/TrashCatWindows/  (see App/README.md)"
Write-Host ""
