# PowerShell Build Script for Windows Power Mode Switcher
$ErrorActionPreference = "Stop"

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "  BUILDING POWER MODE SWITCHER PORTABLE APP" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan

$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"

# 1. Check if compiler exists
if (-not (Test-Path $csc)) {
    Write-Error "C# Compiler (csc.exe) not found at: $csc. Please ensure .NET Framework is installed."
    exit 1
}

# 2. Compile and run Icon Generator
Write-Host "Step 1: Generating application icon..." -ForegroundColor Yellow
if (Test-Path "IconGenerator.cs") {
    # Compile Icon Generator
    & $csc /target:exe /out:IconGenerator.exe /r:System.Drawing.dll /r:System.dll IconGenerator.cs
    
    if (-not (Test-Path "IconGenerator.exe")) {
        Write-Error "Failed to compile IconGenerator.exe!"
        exit 1
    }
    
    # Run Icon Generator to create app.ico
    & .\IconGenerator.exe
    
    if (-not (Test-Path "app.ico")) {
        Write-Error "Failed to generate app.ico!"
        exit 1
    }
    
    # Clean up generator binaries
    Remove-Item "IconGenerator.exe" -Force -ErrorAction SilentlyContinue
    Remove-Item "IconGenerator.cs" -Force -ErrorAction SilentlyContinue
    Write-Host "-> Generated app.ico successfully!" -ForegroundColor Green
} else {
    Write-Host "-> IconGenerator.cs not found, skipping icon generation step." -ForegroundColor DarkYellow
}

# 3. Compile the main application
Write-Host "`nStep 2: Compiling MainWindow Switcher App..." -ForegroundColor Yellow

$source = "Program.cs"
$output = "PowerPlan Switcher.exe"
$manifest = "app.manifest"
$icon = "app.ico"

if (-not (Test-Path $source)) {
    Write-Error "Source file $source not found!"
    exit 1
}

# Base arguments
$args = @("/target:winexe", "/out:$output")

# Reference common assembly DLLs
$args += "/r:System.Windows.Forms.dll"
$args += "/r:System.Drawing.dll"
$args += "/r:System.dll"
$args += "/r:System.Core.dll"

# Embed DPI-awareness manifest if present
if (Test-Path $manifest) {
    Write-Host "-> Embedding DPI-aware app.manifest..." -ForegroundColor Gray
    $args += "/win32manifest:$manifest"
}

# Embed high-resolution icon if present
if (Test-Path $icon) {
    Write-Host "-> Embedding application icon..." -ForegroundColor Gray
    $args += "/win32icon:$icon"
}

# Embed custom transparent PNG power icons as assembly manifest resources
Write-Host "-> Embedding custom transparent PNG plan icons as manifest resources..." -ForegroundColor Gray
$args += "/resource:app.ico,PowerModeSwitcher.app.ico"
$args += "/resource:icons\power-saver.png,PowerModeSwitcher.icons.power-saver.png"
$args += "/resource:icons\balanced.png,PowerModeSwitcher.icons.balanced.png"
$args += "/resource:icons\high-performance.png,PowerModeSwitcher.icons.high-performance.png"
$args += "/resource:icons\ultimate-performance.png,PowerModeSwitcher.icons.ultimate-performance.png"

# Source code
$args += $source

# Run compiler
Write-Host "-> Compiling..." -ForegroundColor Gray
& $csc $args

if (-not (Test-Path $output)) {
    Write-Error "Compilation failed! PowerSwitcher.exe was not created."
    exit 1
}

# Get compiled executable size in KB
$size = [math]::round((Get-Item $output).Length / 1KB, 1)

Write-Host "`n==========================================" -ForegroundColor Green
Write-Host "  SUCCESS! Portable App Compiled successfully." -ForegroundColor Green
Write-Host "  Binary Name : $output" -ForegroundColor Green
Write-Host "  Binary Size : $size KB" -ForegroundColor Green
Write-Host "  Location    : $(Resolve-Path $output)" -ForegroundColor Green
Write-Host "==========================================" -ForegroundColor Green
