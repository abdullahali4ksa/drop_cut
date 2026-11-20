# DropCut Launch & Dev Runner
# Automatically stops previous instance, builds latest code, and launches DropCut.App

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
if (-not $scriptDir) { $scriptDir = "d:\GAI\drop_cut" }
Set-Location $scriptDir

Write-Host ">>> [DropCut] 1/3: Stopping previous instance..." -ForegroundColor Cyan
Stop-Process -Name "DropCut.App", "netcoredbg" -Force -ErrorAction SilentlyContinue

Write-Host ">>> [DropCut] 2/3: Building solution with latest changes..." -ForegroundColor Cyan
dotnet build DropCut.sln
if ($LASTEXITCODE -ne 0) {
    Write-Host ">>> [DropCut] Build failed! Check errors above." -ForegroundColor Red
    exit $LASTEXITCODE
}

$exePath = Join-Path $scriptDir "src\DropCut.App\bin\x64\Debug\net10.0-windows10.0.26100.0\win-x64\DropCut.App.exe"
if (-not (Test-Path $exePath)) {
    $found = Get-ChildItem -Path (Join-Path $scriptDir "src\DropCut.App\bin") -Recurse -Filter "DropCut.App.exe" | Select-Object -First 1
    if ($found) { $exePath = $found.FullName }
}

Write-Host ">>> [DropCut] 3/3: Launching latest version..." -ForegroundColor Green
Start-Process -FilePath $exePath -WorkingDirectory (Split-Path -Parent $exePath)
Write-Host ">>> [DropCut] Running successfully!" -ForegroundColor Green
