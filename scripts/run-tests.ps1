param(
  [string]$Filter = ""
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $repoRoot

function Import-DotEnv {
  param([string]$Path)
  if (-not (Test-Path $Path)) { return }
  Get-Content $Path | ForEach-Object {
    if ($_ -match '^\s*#' -or $_ -match '^\s*$') { return }
    $pair = $_ -split '=', 2
    if ($pair.Count -eq 2) {
      $name = $pair[0].Trim()
      # Keep values already set by the caller (CI env, repo variables). .env only fills gaps.
      if ($null -eq [Environment]::GetEnvironmentVariable($name)) {
        Set-Item -Path "Env:$name" -Value $pair[1].Trim()
      }
    }
  }
}

Import-DotEnv (Join-Path $repoRoot ".env")
if (-not $env:REPO_ROOT) { $env:REPO_ROOT = $repoRoot }
if (-not $env:REPORT_ROOT) { $env:REPORT_ROOT = "reports" }
if (-not $env:TEST_RUN_ID) { $env:TEST_RUN_ID = (Get-Date -Format "yyyyMMdd-HHmmss") }

if ($env:GAME_EXE -and -not [System.IO.Path]::IsPathRooted($env:GAME_EXE)) {
  $env:GAME_EXE = Join-Path $repoRoot ($env:GAME_EXE -replace '/', [System.IO.Path]::DirectorySeparatorChar)
}

function Test-TcpPortOpen {
  param([string]$HostName, [int]$Port, [int]$TimeoutMs = 2000)
  $client = $null
  try {
    $client = New-Object System.Net.Sockets.TcpClient
    $connect = $client.BeginConnect($HostName, $Port, $null, $null)
    $waited = $connect.AsyncWaitHandle.WaitOne($TimeoutMs, $false)
    if ($waited -and $client.Connected) { return $true }
    return $false
  }
  catch {
    return $false
  }
  finally {
    if ($client) { $client.Close() }
  }
}

function Write-Preflight {
  if (-not $env:ALT_DRIVER_PORT) { $env:ALT_DRIVER_PORT = "13000" }
  if (-not $env:ALT_DRIVER_HOST) { $env:ALT_DRIVER_HOST = "127.0.0.1" }

  $gameOk = $env:GAME_EXE -and (Test-Path -LiteralPath $env:GAME_EXE)
  $ffmpegOk = $null -ne (Get-Command ffmpeg -ErrorAction SilentlyContinue)
  $altDesktop = Get-Process -Name "AltTester*" -ErrorAction SilentlyContinue
  $port = [int]$env:ALT_DRIVER_PORT
  $serverOk = Test-TcpPortOpen -HostName $env:ALT_DRIVER_HOST -Port $port

  Write-Host "`n--- Preflight ---"
  Write-Host ("  .NET test project: OK (run dotnet build if you changed tests)")
  Write-Host ("  GAME_EXE:          $(if ($gameOk) { $env:GAME_EXE } else { 'MISSING - build instrumented TrashCat (see App/README.md)' })")
  Write-Host ("  ffmpeg:            $(if ($ffmpegOk) { 'on PATH' } else { 'not found - tests still run; set RECORD_VIDEO=0 or install ffmpeg for MP4' })")
  Write-Host ("  AltTester Desktop: $(if ($altDesktop) { 'process detected' } else { 'not detected - start AltTester Desktop before tests (AltTester 2.x)' })")
  Write-Host ("  AltTester server:  $(if ($serverOk) { "$($env:ALT_DRIVER_HOST):$port listening" } else { "nothing on $($env:ALT_DRIVER_HOST):$port - open AltTester Desktop first" })")
  Write-Host "-----------------`n"

  if (-not $serverOk) {
    Write-Host "AltTester 2.x runs the WebSocket server inside AltTester Desktop. Open the app, then run npm test again.`n"
    exit 1
  }

  if (-not $gameOk) {
    Write-Host "Cannot auto-launch game. Place TrashCat.exe at App/TrashCatWindows/TrashCat.exe or set GAME_EXE in .env.`n"
  }
}
Write-Preflight

$reports = Join-Path $repoRoot $env:REPORT_ROOT
$allureResults = Join-Path $reports "allure-results"
$videos = Join-Path $reports "videos"
New-Item -ItemType Directory -Force -Path $allureResults, $videos | Out-Null
$env:ALLURE_RESULTS_DIRECTORY = $allureResults

$gameProc = $null
$ffmpegProc = $null

try {
  $gameName = if ($env:GAME_EXE) { [System.IO.Path]::GetFileNameWithoutExtension($env:GAME_EXE) } else { $null }
  $gameAlreadyRunning = $false
  if ($gameName) {
    $gameAlreadyRunning = $null -ne (Get-Process -Name $gameName -ErrorAction SilentlyContinue | Where-Object {
      try { $_.Path -eq $env:GAME_EXE } catch { $false }
    } | Select-Object -First 1)
  }

  if ($gameAlreadyRunning) {
    Write-Host "Game already running. Not launching another copy, and this script will not close it."
  } elseif (($env:LAUNCH_GAME -eq "1") -and $env:GAME_EXE -and (Test-Path $env:GAME_EXE)) {
    Write-Host "Launching game: $($env:GAME_EXE)"
    $gameProc = Start-Process -FilePath $env:GAME_EXE -WorkingDirectory (Split-Path $env:GAME_EXE -Parent) -PassThru
    $launchWait = if ($env:GAME_LAUNCH_WAIT_SECS) { [int]$env:GAME_LAUNCH_WAIT_SECS } else { 15 }
    Write-Host "Waiting ${launchWait}s for game to connect to AltTester Desktop..."
    Start-Sleep -Seconds $launchWait
  } else {
    Write-Host "LAUNCH_GAME off or GAME_EXE missing - start the instrumented TrashCat build manually."
  }

  if ($env:FFMPEG_DESKTOP_CAPTURE -eq "1") {
    $ffmpegCmd = Get-Command ffmpeg -ErrorAction SilentlyContinue
    $ffmpeg = if ($ffmpegCmd) { $ffmpegCmd.Source } else { $null }
    if ($ffmpeg) {
      $desktopMp4 = Join-Path $videos "desktop-$($env:TEST_RUN_ID).mp4"
      $ffmpegArgs = @(
        "-y", "-f", "gdigrab", "-framerate", "15", "-i", "desktop",
        "-c:v", "libx264", "-pix_fmt", "yuv420p", $desktopMp4
      )
      Write-Host "Starting ffmpeg desktop capture -> $desktopMp4"
      $ffmpegProc = Start-Process -FilePath $ffmpeg -ArgumentList $ffmpegArgs -PassThru -WindowStyle Hidden
    }
  }

  $testProj = Join-Path $repoRoot "tests\TrashCat.Tests\TrashCat.Tests.csproj"
  $filterArg = @()
  if ($Filter) {
    $filterArg = @("--filter", "Category=$Filter")
  }

  dotnet test $testProj @filterArg --logger "console;verbosity=detailed"
  $exit = $LASTEXITCODE

  if ($ffmpegProc -and -not $ffmpegProc.HasExited) {
    $ffmpegProc | Stop-Process -Force
    $latestDesktop = Join-Path $videos "desktop-$($env:TEST_RUN_ID).mp4"
    if (Test-Path $latestDesktop) {
      Copy-Item $latestDesktop (Join-Path $videos "latest-desktop.mp4") -Force
    }
  }

  & (Join-Path $PSScriptRoot "generate-report.ps1")
  node (Join-Path $PSScriptRoot "generate-alttester-report.js")

  exit $exit
}
finally {
  if ($gameProc -and -not $gameProc.HasExited) {
    Stop-Process -Id $gameProc.Id -Force -ErrorAction SilentlyContinue
  }
}
