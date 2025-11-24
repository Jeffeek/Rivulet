#!/usr/bin/env pwsh

# Rivulet Package Management - Update all generated files
# This script regenerates all documentation and configuration files from packages.yml
# See PACKAGE_MANAGEMENT.md for details

param(
    [Parameter(Mandatory=$false)]
    [switch]$Verbose
)

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Rivulet Package Management" -ForegroundColor Cyan
Write-Host "Updating all generated files..." -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Navigate to repository root
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location (Join-Path $ScriptDir "..")

# Check if Python is available
$pythonCmd = $null
if (Get-Command python3 -ErrorAction SilentlyContinue) {
    $pythonCmd = "python3"
} elseif (Get-Command python -ErrorAction SilentlyContinue) {
    $pythonCmd = "python"
} else {
    Write-Host "❌ Error: Python 3 is required but not found" -ForegroundColor Red
    Write-Host "   Please install Python 3.8 or later" -ForegroundColor Red
    exit 1
}

# Check if PyYAML is installed
$yamlCheck = & $pythonCmd -c "import yaml" 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Host "⚠️  PyYAML not found. Installing..." -ForegroundColor Yellow
    & $pythonCmd -m pip install --quiet pyyaml
    if ($LASTEXITCODE -ne 0) {
        Write-Host "❌ Failed to install PyYAML" -ForegroundColor Red
        Write-Host "   Please run: pip install pyyaml" -ForegroundColor Red
        exit 1
    }
    Write-Host "✅ PyYAML installed" -ForegroundColor Green
    Write-Host ""
}

# Run validation first
Write-Host "Step 1: Validating package registry..." -ForegroundColor Cyan
& $pythonCmd scripts/package_registry.py
if ($LASTEXITCODE -ne 0) {
    Write-Host ""
    Write-Host "❌ Package registry validation failed" -ForegroundColor Red
    Write-Host "   Please fix the errors in packages.yml" -ForegroundColor Red
    exit 1
}
Write-Host ""

# Generate all files
Write-Host "Step 2: Generating files..." -ForegroundColor Cyan
$generateArgs = @("scripts/generate-all.py")
if ($Verbose) {
    $generateArgs += "--verbose"
}

& $pythonCmd $generateArgs
if ($LASTEXITCODE -ne 0) {
    Write-Host ""
    Write-Host "❌ File generation failed" -ForegroundColor Red
    exit 1
}
Write-Host ""

Write-Host "========================================" -ForegroundColor Green
Write-Host "✅ All files updated successfully!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
Write-Host "Generated files:"
Write-Host "  - README.md (package list)"
Write-Host "  - samples/README.md"
Write-Host "  - docs/ROADMAP.md (or ROADMAP.md)"
Write-Host "  - .github/workflows/release.yml"
Write-Host "  - .github/workflows/nuget-activity-monitor.yml"
Write-Host "  - .github/dependabot.yml"
Write-Host ""
Write-Host "Next steps:"
Write-Host "  1. Review the changes: git diff"
Write-Host "  2. Commit the changes: git add packages.yml README.md samples/README.md ROADMAP.md .github/ && git commit -m 'Update generated files'"
Write-Host ""
