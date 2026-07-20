# Throughput - Publish Application Folder (for the installer)
# ReadyToRun, NOT single-file. This is what Setup.exe ships.
#
# Why not the single-file build? That bundle extracts ~16MB of native libraries
# to %TEMP% on the first run after every update (~4.6s) and JITs all code, which
# roughly doubles committed memory. This folder build starts in ~1s, never pays
# an extraction cost, and uses less RAM. The portable download stays single-file.

$ErrorActionPreference = "Stop"

Write-Host "Building Throughput v3.1.1 - Application Folder (ReadyToRun)" -ForegroundColor Cyan
Write-Host ("=" * 50)

$publishDir = ".\publish\app"
if (Test-Path $publishDir) {
    Write-Host "Cleaning previous build..." -ForegroundColor Yellow
    Remove-Item -Path $publishDir -Recurse -Force
}

Write-Host "Building..." -ForegroundColor Green
dotnet publish -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=false `
    -p:PublishReadyToRun=true `
    -o $publishDir

if ($LASTEXITCODE -eq 0) {
    $sizeMB = [math]::Round(((Get-ChildItem $publishDir -Recurse | Measure-Object -Property Length -Sum).Sum / 1MB), 2)

    Write-Host ""
    Write-Host "Build successful!" -ForegroundColor Green
    Write-Host "Output: $publishDir"
    Write-Host "Size: $sizeMB MB"
    Write-Host ""
    Write-Host "Now compile Installer\setup.iss with Inno Setup to produce Setup.exe." -ForegroundColor Cyan
}
else {
    Write-Host "Build failed!" -ForegroundColor Red
    exit 1
}
