# Start or stop AltTester Desktop's server for a CI run.
# Start: if port 13000 is already open, leave that process alone.
#        Otherwise launch AltTesterDesktop.exe in batch mode and wait for the port.
# Stop:  close the process only when this script started it.
param(
  [Parameter(Mandatory = $true)]
  [ValidateSet('Start', 'Stop')]
  [string]$Action
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
      if ($null -eq [Environment]::GetEnvironmentVariable($name)) {
        Set-Item -Path "Env:$name" -Value $pair[1].Trim()
      }
    }
  }
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

function Stop-ProcessTree {
  param([int]$ProcessId)
  $children = @(Get-CimInstance Win32_Process -Filter "ParentProcessId=$ProcessId" -ErrorAction SilentlyContinue)
  foreach ($child in $children) {
    Stop-ProcessTree -ProcessId ([int]$child.ProcessId)
  }
  Stop-Process -Id $ProcessId -Force -ErrorAction SilentlyContinue
}

function Resolve-AltTesterExe {
  $candidates = @()
  if ($env:ALTTESTER_DESKTOP_EXE) { $candidates += $env:ALTTESTER_DESKTOP_EXE }
  $candidates += @(
    "$env:ProgramFiles\AltTesterDesktop\AltTesterDesktop.exe",
    "${env:ProgramFiles(x86)}\AltTesterDesktop\AltTesterDesktop.exe",
    "$env:LOCALAPPDATA\AltTesterDesktop\AltTesterDesktop.exe"
  )
  foreach ($path in $candidates) {
    if ($path -and (Test-Path -LiteralPath $path)) { return $path }
  }
  return $null
}

Import-DotEnv (Join-Path $repoRoot ".env")
if (-not $env:ALT_DRIVER_HOST) { $env:ALT_DRIVER_HOST = "127.0.0.1" }
if (-not $env:ALT_DRIVER_PORT) { $env:ALT_DRIVER_PORT = "13000" }
if (-not $env:REPORT_ROOT) { $env:REPORT_ROOT = "reports" }

$reports = Join-Path $repoRoot $env:REPORT_ROOT
$stateFile = Join-Path $reports ".ci-alttester-server.json"
$port = [int]$env:ALT_DRIVER_PORT
$hostName = $env:ALT_DRIVER_HOST

if ($Action -eq 'Stop') {
  if (-not (Test-Path -LiteralPath $stateFile)) {
    Write-Host "No CI AltTester session to stop."
    exit 0
  }

  $state = Get-Content -LiteralPath $stateFile -Raw | ConvertFrom-Json
  if ($state.startedByCi -and $state.pid) {
    Write-Host "Stopping AltTester Desktop started by CI (pid $($state.pid))."
    Stop-ProcessTree -ProcessId ([int]$state.pid)
  } else {
    Write-Host "AltTester was already running before CI. Leaving it open."
  }

  Remove-Item -LiteralPath $stateFile -Force -ErrorAction SilentlyContinue
  exit 0
}

New-Item -ItemType Directory -Force -Path $reports | Out-Null

if (Test-TcpPortOpen -HostName $hostName -Port $port) {
  Write-Host "AltTester server already listening on ${hostName}:${port}. Leaving that process running."
  @{ startedByCi = $false; pid = $null } | ConvertTo-Json | Set-Content -LiteralPath $stateFile -Encoding utf8
  exit 0
}

$exe = Resolve-AltTesterExe
if (-not $exe) {
  Write-Host "AltTester Desktop was not found."
  Write-Host "Install it on this machine, or set ALTTESTER_DESKTOP_EXE to the full path of AltTesterDesktop.exe."
  exit 1
}

$logDir = Join-Path $reports "logs"
New-Item -ItemType Directory -Force -Path $logDir | Out-Null
$logFile = Join-Path $logDir "alttester-server.log"

$serverArgs = @('-batchmode', '-port', "$port", '-nographics', '-logfile', $logFile)
if ($env:ALTTESTER_LICENSE) {
  $serverArgs += @('-license', $env:ALTTESTER_LICENSE)
}

Write-Host "Starting AltTester Desktop: $exe"
Write-Host "Server log: $logFile"
$proc = Start-Process -FilePath $exe -ArgumentList $serverArgs -WorkingDirectory (Split-Path $exe -Parent) -PassThru -WindowStyle Hidden

@{ startedByCi = $true; pid = $proc.Id } | ConvertTo-Json | Set-Content -LiteralPath $stateFile -Encoding utf8

$waitSecs = if ($env:ALTTESTER_START_WAIT_SECS) { [int]$env:ALTTESTER_START_WAIT_SECS } else { 60 }
$deadline = (Get-Date).AddSeconds($waitSecs)
while ((Get-Date) -lt $deadline) {
  if (Test-TcpPortOpen -HostName $hostName -Port $port) {
    Write-Host "AltTester server is listening on ${hostName}:${port}."
    exit 0
  }
  if ($proc.HasExited) {
    Write-Host "AltTester Desktop exited before the server port opened. See $logFile"
    Remove-Item -LiteralPath $stateFile -Force -ErrorAction SilentlyContinue
    exit 1
  }
  Start-Sleep -Seconds 2
}

Write-Host "AltTester Desktop did not open port $port within ${waitSecs}s. See $logFile"
Write-Host "If this copy of AltTester is not licensed yet, add the ALTTESTER_LICENSE repository secret and run the workflow again."
Stop-ProcessTree -ProcessId $proc.Id
Remove-Item -LiteralPath $stateFile -Force -ErrorAction SilentlyContinue
exit 1
