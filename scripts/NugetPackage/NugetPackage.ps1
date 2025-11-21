param(
    [Parameter(Mandatory=$false)]
    [string]$Version = "1.0.0-local-test",

    [Parameter(Mandatory=$false)]
    [ValidateSet("Core", "Diagnostics", "DiagnosticsOpenTelemetry", "Testing", "Hosting", "Http", "Polly" "All")]
    [string]$Project = "All"
)

$ErrorActionPreference = "Stop"

# Navigate to repository root (2 levels up from scripts/NugetPackage/)
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location (Join-Path $ScriptDir "../..")

Write-Host "======================================" -ForegroundColor Cyan
Write-Host "  NuGet Package Builder & Inspector" -ForegroundColor Cyan
Write-Host "======================================" -ForegroundColor Cyan
Write-Host ""

# Define project paths
$Projects = @{
    "Core" = "src\Rivulet.Core\Rivulet.Core.csproj"
    "Diagnostics" = "src\Rivulet.Diagnostics\Rivulet.Diagnostics.csproj"
    "DiagnosticsOpenTelemetry" = "src\Rivulet.Diagnostics.OpenTelemetry\Rivulet.Diagnostics.OpenTelemetry.csproj"
    "Testing" = "src\Rivulet.Testing\Rivulet.Testing.csproj"
    "Hosting" = "src\Rivulet.Hosting\Rivulet.Hosting.csproj"
    "Http" = "src\Rivulet.Http\Rivulet.Http.csproj"
    "Polly" = "src\Rivulet.Http\Rivulet.Polly.csproj"
}

# Determine which projects to pack
$ProjectsToPack = if ($Project -eq "All") {
    $Projects.Values
} else {
    @($Projects[$Project])
}

$OutputDir = ".\test-packages"
$ExtractDir = ".\test-extract"

Write-Host "Version:       $Version" -ForegroundColor Green
Write-Host "Projects:      $Project ($($ProjectsToPack.Count) package(s))" -ForegroundColor Green
Write-Host "Output Dir:    $OutputDir" -ForegroundColor Green
Write-Host "Extract Dir:   $ExtractDir" -ForegroundColor Green
Write-Host ""

# Clean up previous builds
if (Test-Path $OutputDir) {
    Write-Host "Cleaning up previous packages..." -ForegroundColor Yellow
    Remove-Item $OutputDir -Recurse -Force
}

if (Test-Path $ExtractDir) {
    Write-Host "Cleaning up previous extracts..." -ForegroundColor Yellow
    Remove-Item $ExtractDir -Recurse -Force
}

# Create output directory
New-Item -ItemType Directory -Path $OutputDir | Out-Null

# Build all packages
$packageCount = 0
foreach ($projectPath in $ProjectsToPack) {
    $packageCount++
    $projectName = [System.IO.Path]::GetFileNameWithoutExtension($projectPath)

    Write-Host ""
    Write-Host "======================================" -ForegroundColor Yellow
    Write-Host "  Building $projectName ($packageCount/$($ProjectsToPack.Count))" -ForegroundColor Yellow
    Write-Host "======================================" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Command: dotnet pack $projectPath -c Release --output $OutputDir -p:PackageVersion=$Version" -ForegroundColor Gray
    Write-Host ""

    dotnet pack $projectPath -c Release --output $OutputDir -p:PackageVersion=$Version

    if ($LASTEXITCODE -ne 0) {
        Write-Host ""
        Write-Host "Error: Package build failed for $projectName!" -ForegroundColor Red
        exit 1
    }

    Write-Host ""
    Write-Host "$projectName package built successfully!" -ForegroundColor Green
}

Write-Host ""
Write-Host "======================================" -ForegroundColor Green
Write-Host "  All packages built successfully!" -ForegroundColor Green
Write-Host "======================================" -ForegroundColor Green
Write-Host ""

# List the created packages
Write-Host "Created packages:" -ForegroundColor Yellow
Get-ChildItem $OutputDir -Filter *.nupkg | ForEach-Object {
    $size = "{0:N2}" -f ($_.Length / 1KB)
    Write-Host "  $($_.Name) ($size KB)" -ForegroundColor Cyan
}

Write-Host ""
Write-Host "Inspecting packages..." -ForegroundColor Yellow

# Inspect each package
foreach ($nupkgFile in Get-ChildItem $OutputDir -Filter *.nupkg) {
    $packageName = [System.IO.Path]::GetFileNameWithoutExtension($nupkgFile.Name)
    $extractPath = Join-Path $ExtractDir $packageName

    Write-Host ""
    Write-Host "Extracting $($nupkgFile.Name)..." -ForegroundColor Yellow

    # Copy .nupkg to .zip (required by Expand-Archive)
    $zipPath = "$($nupkgFile.FullName).zip"
    Copy-Item $nupkgFile.FullName $zipPath

    # Extract the package
    New-Item -ItemType Directory -Path $extractPath | Out-Null
    Expand-Archive $zipPath -DestinationPath $extractPath
    Remove-Item $zipPath

    Write-Host "Package contents for $packageName`:" -ForegroundColor Cyan
    Write-Host ""

    # Show key files only
    Get-ChildItem $extractPath -Recurse -Include *.dll,*.xml,README.md,*.png | ForEach-Object {
        $relativePath = $_.Name
        $size = "{0:N2}" -f ($_.Length / 1KB)
        Write-Host "  $relativePath ($size KB)" -ForegroundColor Gray
    }
}

Write-Host ""
Write-Host "======================================" -ForegroundColor Green
Write-Host "  Package inspection completed!" -ForegroundColor Green
Write-Host "======================================" -ForegroundColor Green
Write-Host ""

# Verification summary
Write-Host "Verification:" -ForegroundColor Yellow
$allPackagesValid = $true

foreach ($nupkgFile in Get-ChildItem $OutputDir -Filter *.nupkg) {
    $packageName = [System.IO.Path]::GetFileNameWithoutExtension($nupkgFile.Name)
    $extractPath = Join-Path $ExtractDir $packageName

    Write-Host "  $packageName`:" -ForegroundColor Cyan

    # Check for expected files
    $expectedFiles = @("README.md", "nuget_logo.png")
    foreach ($file in $expectedFiles) {
        $found = Get-ChildItem $extractPath -Recurse -Filter $file -ErrorAction SilentlyContinue
        if ($found) {
            Write-Host "    [OK] $file found" -ForegroundColor Green
        } else {
            Write-Host "    [MISSING] $file not found!" -ForegroundColor Red
            $allPackagesValid = $false
        }
    }

    # Check for DLL files
    # Extract project name without version (e.g., "Rivulet.Core.1.0.0-local-test" -> "Rivulet.Core")
    $projectName = $packageName -replace '\.\d+\.\d+\.\d+.*$', ''
    $dllName = "$projectName.dll"
    $dllFiles = Get-ChildItem $extractPath -Recurse -Filter $dllName
    if ($dllFiles) {
        Write-Host "    [OK] $dllName found in $($dllFiles.Count) target(s)" -ForegroundColor Green
    } else {
        Write-Host "    [MISSING] $dllName not found!" -ForegroundColor Red
        $allPackagesValid = $false
    }
}

Write-Host ""

if ($allPackagesValid) {
    Write-Host "All packages valid!" -ForegroundColor Green
} else {
    Write-Host "Warning: Some packages have missing files." -ForegroundColor Yellow
}

Write-Host ""
Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "  - Build specific package:  .\NugetPackage.ps1 -Project Core" -ForegroundColor Gray
Write-Host "                             .\NugetPackage.ps1 -Project Diagnostics" -ForegroundColor Gray
Write-Host "                             .\NugetPackage.ps1 -Project DiagnosticsOpenTelemetry" -ForegroundColor Gray
Write-Host "                             .\NugetPackage.ps1 -Project Testing" -ForegroundColor Gray
Write-Host "                             .\NugetPackage.ps1 -Project Hosting" -ForegroundColor Gray
Write-Host "                             .\NugetPackage.ps1 -Project Http" -ForegroundColor Gray
Write-Host "                             .\NugetPackage.ps1 -Project Polly" -ForegroundColor Gray
Write-Host "  - Build all packages:      .\NugetPackage.ps1 -Project All" -ForegroundColor Gray
Write-Host "  - Test locally:            dotnet add package Rivulet.Core --source ./test-packages" -ForegroundColor Gray
Write-Host ""
