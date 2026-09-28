# Quick environment check before first npm test
$ErrorActionPreference = "Continue"
$repoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $repoRoot

function Import-DotEnv {
  param([string]$Path)
  if (-not (Test-Path $Path)) { return }
  Get-Content $Path | ForEach-Object {
    if ($_ -match '^\s*#' -or $_ -match '^\s*$') { return }
    $pair = $_ -split '=', 2
    if ($pair.Count -eq 2) {
      Set-Item -Path "Env:$($pair[0].Trim())" -Value $pair[1].Trim()
    }
  }
}

Import-DotEnv (Join-Path $repoRoot ".env")
if ($env:GAME_EXE -and -not [System.IO.Path]::IsPathRooted($env:GAME_EXE)) {
  $env:GAME_EXE = Join-Path $repoRoot ($env:GAME_EXE -replace '/', [System.IO.Path]::DirectorySeparatorChar)
}

$checks = @(
  @{ Name = ".NET SDK"; Ok = $null -ne (Get-Command dotnet -ErrorAction SilentlyContinue); Detail = (dotnet --version 2>$null) },
  @{ Name = "Node.js"; Ok = $null -ne (Get-Command node -ErrorAction SilentlyContinue); Detail = (node -v 2>$null) },
  @{ Name = ".env file"; Ok = (Test-Path (Join-Path $repoRoot ".env")); Detail = "" },
  @{ Name = "TrashCat.exe"; Ok = $env:GAME_EXE -and (Test-Path -LiteralPath $env:GAME_EXE); Detail = $env:GAME_EXE },
  @{ Name = "ffmpeg (optional MP4)"; Ok = $null -ne (Get-Command ffmpeg -ErrorAction SilentlyContinue); Detail = "" },
  @{ Name = "AltTester Desktop running"; Ok = $null -ne (Get-Process -Name "AltTester*" -ErrorAction SilentlyContinue); Detail = "Start AltTester Desktop before npm test (AltTester 2.x server on port 13000)" }
)

Write-Host "`nFirst-run preflight - $repoRoot`n"
foreach ($c in $checks) {
  $mark = if ($c.Ok) { "[OK]" } else { "[--]" }
  $extra = if ($c.Detail) { " - $($c.Detail)" } else { "" }
  Write-Host "$mark $($c.Name)$extra"
}

$gameReady = ($checks | Where-Object { $_.Name -eq "TrashCat.exe" }).Ok
if (-not $gameReady) {
  Write-Host "`nNext step: instrument TrashCat in Unity and build to App/TrashCatWindows/ - see App/README.md`n"
  exit 1
}

Write-Host "`nReady for: npm test`n"
exit 0
