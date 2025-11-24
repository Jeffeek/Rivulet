#!/usr/bin/env pwsh

# Rivulet Package Management - Update all generated files
# This script regenerates all documentation and configuration files from packages.yml
# See PACKAGE_MANAGEMENT.md for details

param(
    [Parameter(Mandatory=$false)]
    [switch]$IsVerbose
)

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Rivulet Package Management" -ForegroundColor Cyan
Write-Host "Updating all generated files..." -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Navigate to repository root
# Script is in scripts/UpdateAll/, so go up two levels to reach repo root
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location (Join-Path $ScriptDir ".." | Join-Path -ChildPath "..")

# Check if Python is available and intelligently select the best one
$pythonCmd = $null
$python3Available = $false
$pythonAvailable = $false
$python3HasYaml = $false
$pythonHasYaml = $false

# Check python3
if (Get-Command python3 -ErrorAction SilentlyContinue) {
    $python3Available = $true
    $null = & python3 -c "import yaml" 2>&1
    if ($LASTEXITCODE -eq 0) {
        $python3HasYaml = $true
    }
}

# Check python
if (Get-Command python -ErrorAction SilentlyContinue) {
    $pythonAvailable = $true
    $null = & python -c "import yaml" 2>&1
    if ($LASTEXITCODE -eq 0) {
        $pythonHasYaml = $true
    }
}

# Exit if no Python found
if (-not $python3Available -and -not $pythonAvailable) {
    Write-Host "❌ Error: Python 3 is required but not found" -ForegroundColor Red
    Write-Host "   Please install Python 3.8 or later" -ForegroundColor Red
    exit 1
}

# Prefer the Python command that already has PyYAML installed
if ($python3HasYaml) {
    $pythonCmd = "python3"
} elseif ($pythonHasYaml) {
    $pythonCmd = "python"
} elseif ($python3Available) {
    $pythonCmd = "python3"
} else {
    $pythonCmd = "python"
}

# Install PyYAML if needed
$null = & $pythonCmd -c "import yaml" 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Host "⚠️  PyYAML not found. Installing..." -ForegroundColor Yellow
    & $pythonCmd -m pip install --user --quiet pyyaml
    if ($LASTEXITCODE -ne 0) {
        Write-Host "❌ Failed to install PyYAML" -ForegroundColor Red
        Write-Host "   Please run: $pythonCmd -m pip install pyyaml" -ForegroundColor Red
        exit 1
    }
    Write-Host "✅ PyYAML installed" -ForegroundColor Green
    Write-Host ""
}

# Run validation first
Write-Host "Step 1: Validating package registry..." -ForegroundColor Cyan
& $pythonCmd scripts/UpdateAll/package_registry.py
if ($LASTEXITCODE -ne 0) {
    Write-Host ""
    Write-Host "❌ Package registry validation failed" -ForegroundColor Red
    Write-Host "   Please fix the errors in packages.yml" -ForegroundColor Red
    exit 1
}
Write-Host ""

# Generate all files
Write-Host "Step 2: Generating files..." -ForegroundColor Cyan
$generateArgs = @("scripts/UpdateAll/generate-all.py")
if ($IsVerbose) {
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
