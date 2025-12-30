# Throughput - Publish Portable Executable
# Creates a single self-contained EXE file

$ErrorActionPreference = "Stop"

Write-Host "Building Throughput v2.0.0 - Portable Edition" -ForegroundColor Cyan
Write-Host "=" * 50

# Clean previous builds
$publishDir = ".\publish\portable"
if (Test-Path $publishDir) {
    Write-Host "Cleaning previous build..." -ForegroundColor Yellow
    Remove-Item -Path $publishDir -Recurse -Force
}

# Build and publish
Write-Host "Building..." -ForegroundColor Green
dotnet publish -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -o $publishDir

if ($LASTEXITCODE -eq 0) {
    $exePath = Join-Path $publishDir "Throughput.exe"
    $fileInfo = Get-Item $exePath
    $sizeMB = [math]::Round($fileInfo.Length / 1MB, 2)
    
    Write-Host ""
    Write-Host "Build successful!" -ForegroundColor Green
    Write-Host "Output: $exePath"
    Write-Host "Size: $sizeMB MB"
    Write-Host ""
    Write-Host "This is a portable executable - no installation required." -ForegroundColor Cyan
} else {
    Write-Host "Build failed!" -ForegroundColor Red
    exit 1
}
