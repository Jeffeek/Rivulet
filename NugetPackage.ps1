param(
    [Parameter(Mandatory=$false)]
    [string]$Version = "1.0.0-local-test"
)

$ErrorActionPreference = "Stop"

Write-Host "======================================" -ForegroundColor Cyan
Write-Host "  NuGet Package Builder & Inspector" -ForegroundColor Cyan
Write-Host "======================================" -ForegroundColor Cyan
Write-Host ""

$ProjectPath = "src\Rivulet.Core\Rivulet.Core.csproj"
$OutputDir = ".\test-packages"
$ExtractDir = ".\test-extract"
$PackageName = "Rivulet.Core.$Version"

Write-Host "Version:      $Version" -ForegroundColor Green
Write-Host "Project:      $ProjectPath" -ForegroundColor Green
Write-Host "Output Dir:   $OutputDir" -ForegroundColor Green
Write-Host "Extract Dir:  $ExtractDir" -ForegroundColor Green
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

# Build the package
Write-Host ""
Write-Host "Building NuGet package..." -ForegroundColor Yellow
Write-Host "Command: dotnet pack $ProjectPath -c Release --output $OutputDir -p:PackageVersion=$Version" -ForegroundColor Gray
Write-Host ""

dotnet pack $ProjectPath -c Release --output $OutputDir -p:PackageVersion=$Version

if ($LASTEXITCODE -ne 0) {
    Write-Host ""
    Write-Host "Error: Package build failed!" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "Package built successfully!" -ForegroundColor Green
Write-Host ""

# List the created packages
Write-Host "Created packages:" -ForegroundColor Yellow
Get-ChildItem $OutputDir -Filter *.nupkg | ForEach-Object {
    $size = "{0:N2}" -f ($_.Length / 1KB)
    Write-Host "  $($_.Name) ($size KB)" -ForegroundColor Cyan
}

Write-Host ""
Write-Host "Extracting package for inspection..." -ForegroundColor Yellow

# Copy .nupkg to .zip (required by Expand-Archive)
$nupkgPath = "$OutputDir\$PackageName.nupkg"
$zipPath = "$OutputDir\$PackageName.zip"

if (-not (Test-Path $nupkgPath)) {
    Write-Host "Error: Package file not found: $nupkgPath" -ForegroundColor Red
    exit 1
}

Copy-Item $nupkgPath $zipPath

# Extract the package
New-Item -ItemType Directory -Path $ExtractDir | Out-Null
Expand-Archive $zipPath -DestinationPath $ExtractDir

Write-Host ""
Write-Host "Package contents:" -ForegroundColor Yellow
Write-Host ""

# Show directory tree
Get-ChildItem $ExtractDir -Recurse | ForEach-Object {
    $indent = "  " * ($_.FullName.Split([IO.Path]::DirectorySeparatorChar).Count - $ExtractDir.Split([IO.Path]::DirectorySeparatorChar).Count - 1)
    if ($_.PSIsContainer) {
        Write-Host "$indent$($_.Name)\" -ForegroundColor Yellow
    } else {
        $size = "{0:N2}" -f ($_.Length / 1KB)
        Write-Host "$indent$($_.Name) ($size KB)" -ForegroundColor Gray
    }
}

Write-Host ""
Write-Host "======================================" -ForegroundColor Green
Write-Host "  Package inspection completed!" -ForegroundColor Green
Write-Host "======================================" -ForegroundColor Green
Write-Host ""
Write-Host "Package file:    $OutputDir\$PackageName.nupkg" -ForegroundColor Cyan
Write-Host "Extracted to:    $ExtractDir" -ForegroundColor Cyan
Write-Host ""

# Check for expected files
Write-Host "Verification:" -ForegroundColor Yellow
$expectedFiles = @("nuget_logo.png", "README.md")
$allFound = $true

foreach ($file in $expectedFiles) {
    $found = Get-ChildItem $ExtractDir -Recurse -Filter $file -ErrorAction SilentlyContinue
    if ($found) {
        Write-Host "  [OK] $file found" -ForegroundColor Green
    } else {
        Write-Host "  [MISSING] $file not found!" -ForegroundColor Red
        $allFound = $false
    }
}

# Check for DLL files
$dllFiles = Get-ChildItem $ExtractDir -Recurse -Filter "Rivulet.Core.dll"
if ($dllFiles) {
    Write-Host "  [OK] Library DLL found in $($dllFiles.Count) target(s)" -ForegroundColor Green
} else {
    Write-Host "  [MISSING] Rivulet.Core.dll not found!" -ForegroundColor Red
    $allFound = $false
}

Write-Host ""

if ($allFound) {
    Write-Host "All expected files present!" -ForegroundColor Green
} else {
    Write-Host "Warning: Some expected files are missing from the package." -ForegroundColor Yellow
}

Write-Host ""
