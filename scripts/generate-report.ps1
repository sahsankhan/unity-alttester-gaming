$ErrorActionPreference = "Continue"
$repoRoot = Split-Path -Parent $PSScriptRoot
$reports = Join-Path $repoRoot "reports"
$allureResults = Join-Path $reports "allure-results"
$allureReport = Join-Path $reports "allure-report"

if (-not (Test-Path $allureResults)) {
  Write-Host "No Allure results at $allureResults"
  exit 0
}

$allure = Get-Command allure -ErrorAction SilentlyContinue
if ($allure) {
  & allure generate $allureResults -o $allureReport --clean
  Write-Host "Allure report: $allureReport\index.html"
} else {
  Write-Host "Allure CLI not installed - use npm run report:html or install Allure (scoop/choco)."
}
