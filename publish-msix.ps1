# Throughput - Build for MSIX Packaging
# Prepares the application for MSIX packaging

$ErrorActionPreference = "Stop"

Write-Host "Building Throughput v2.0.0 - MSIX Preparation" -ForegroundColor Cyan
Write-Host "=" * 50

# Clean previous builds
$publishDir = ".\publish\msix-layout"
if (Test-Path $publishDir) {
    Write-Host "Cleaning previous build..." -ForegroundColor Yellow
    Remove-Item -Path $publishDir -Recurse -Force
}

# Build without single file (MSIX handles bundling)
Write-Host "Building..." -ForegroundColor Green
dotnet publish -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=false `
    -o $publishDir

if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed!" -ForegroundColor Red
    exit 1
}

# Copy packaging assets if they exist
$assetsSource = ".\Packaging\Assets"
$assetsDest = "$publishDir\Assets"

if (Test-Path $assetsSource) {
    Write-Host "Copying MSIX assets..." -ForegroundColor Green
    Copy-Item -Path $assetsSource -Destination $assetsDest -Recurse -Force
}

# Copy manifest
$manifestSource = ".\Packaging\Package.appxmanifest"
if (Test-Path $manifestSource) {
    Copy-Item -Path $manifestSource -Destination $publishDir -Force
}

Write-Host ""
Write-Host "Build successful!" -ForegroundColor Green
Write-Host "Layout directory: $publishDir"
Write-Host ""
Write-Host "Next steps to create MSIX package:" -ForegroundColor Yellow
Write-Host "1. Add required assets to $assetsDest"
Write-Host "2. Use MakeAppx.exe or Visual Studio to create the MSIX"
Write-Host "3. Sign the package with a certificate"
Write-Host ""
Write-Host "Quick package command (requires Windows SDK):" -ForegroundColor Cyan
Write-Host "  MakeAppx.exe pack /d $publishDir /p Throughput.msix"
