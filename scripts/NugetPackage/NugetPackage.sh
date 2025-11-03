#!/bin/bash

set -e

# ANSI color codes
CYAN='\033[0;36m'
GREEN='\033[0;32m'
YELLOW='\033[0;33m'
RED='\033[0;31m'
GRAY='\033[0;90m'
NC='\033[0m' # No Color

# Default parameters
VERSION="${1:-1.0.0-local-test}"
PROJECT="${2:-All}"

echo -e "${CYAN}======================================${NC}"
echo -e "${CYAN}  NuGet Package Builder & Inspector${NC}"
echo -e "${CYAN}======================================${NC}"
echo ""

# Define project paths
declare -A PROJECTS
PROJECTS["Core"]="src/Rivulet.Core/Rivulet.Core.csproj"
PROJECTS["Diagnostics"]="src/Rivulet.Diagnostics/Rivulet.Diagnostics.csproj"
PROJECTS["DiagnosticsOpenTelemetry"]="src/Rivulet.Diagnostics.OpenTelemetry/Rivulet.Diagnostics.OpenTelemetry.csproj"
PROJECTS["Testing"]="src/Rivulet.Testing/Rivulet.Testing.csproj"
PROJECTS["Hosting"]="src/Rivulet.Hosting/Rivulet.Hosting.csproj"

# Validate project parameter
VALID_PROJECTS="Core Diagnostics DiagnosticsOpenTelemetry Testing Hosting All"
if [[ ! " $VALID_PROJECTS " =~ " $PROJECT " ]]; then
    echo -e "${RED}Error: Invalid project '$PROJECT'${NC}"
    echo -e "${YELLOW}Valid projects: $VALID_PROJECTS${NC}"
    exit 1
fi

# Determine which projects to pack
if [ "$PROJECT" == "All" ]; then
    PROJECTS_TO_PACK=("${PROJECTS[@]}")
else
    PROJECTS_TO_PACK=("${PROJECTS[$PROJECT]}")
fi

OUTPUT_DIR="./test-packages"
EXTRACT_DIR="./test-extract"

echo -e "${GREEN}Version:       $VERSION${NC}"
echo -e "${GREEN}Projects:      $PROJECT (${#PROJECTS_TO_PACK[@]} package(s))${NC}"
echo -e "${GREEN}Output Dir:    $OUTPUT_DIR${NC}"
echo -e "${GREEN}Extract Dir:   $EXTRACT_DIR${NC}"
echo ""

# Clean up previous builds
if [ -d "$OUTPUT_DIR" ]; then
    echo -e "${YELLOW}Cleaning up previous packages...${NC}"
    rm -rf "$OUTPUT_DIR"
fi

if [ -d "$EXTRACT_DIR" ]; then
    echo -e "${YELLOW}Cleaning up previous extracts...${NC}"
    rm -rf "$EXTRACT_DIR"
fi

# Create output directory
mkdir -p "$OUTPUT_DIR"

# Build all packages
PACKAGE_COUNT=0
TOTAL_PACKAGES=${#PROJECTS_TO_PACK[@]}

for PROJECT_PATH in "${PROJECTS_TO_PACK[@]}"; do
    ((PACKAGE_COUNT++))
    PROJECT_NAME=$(basename "$PROJECT_PATH" .csproj)

    echo ""
    echo -e "${YELLOW}======================================${NC}"
    echo -e "${YELLOW}  Building $PROJECT_NAME ($PACKAGE_COUNT/$TOTAL_PACKAGES)${NC}"
    echo -e "${YELLOW}======================================${NC}"
    echo ""
    echo -e "${GRAY}Command: dotnet pack $PROJECT_PATH -c Release --output $OUTPUT_DIR -p:PackageVersion=$VERSION${NC}"
    echo ""

    if ! dotnet pack "$PROJECT_PATH" -c Release --output "$OUTPUT_DIR" -p:PackageVersion="$VERSION"; then
        echo ""
        echo -e "${RED}Error: Package build failed for $PROJECT_NAME!${NC}"
        exit 1
    fi

    echo ""
    echo -e "${GREEN}$PROJECT_NAME package built successfully!${NC}"
done

echo ""
echo -e "${GREEN}======================================${NC}"
echo -e "${GREEN}  All packages built successfully!${NC}"
echo -e "${GREEN}======================================${NC}"
echo ""

# List the created packages
echo -e "${YELLOW}Created packages:${NC}"
for NUPKG in "$OUTPUT_DIR"/*.nupkg; do
    if [ -f "$NUPKG" ]; then
        SIZE=$(du -k "$NUPKG" | cut -f1)
        SIZE_KB=$(echo "scale=2; $SIZE" | bc)
        echo -e "${CYAN}  $(basename "$NUPKG") ($SIZE_KB KB)${NC}"
    fi
done

echo ""
echo -e "${YELLOW}Inspecting packages...${NC}"

# Inspect each package
for NUPKG in "$OUTPUT_DIR"/*.nupkg; do
    if [ -f "$NUPKG" ]; then
        PACKAGE_NAME=$(basename "$NUPKG" .nupkg)
        EXTRACT_PATH="$EXTRACT_DIR/$PACKAGE_NAME"

        echo ""
        echo -e "${YELLOW}Extracting $(basename "$NUPKG")...${NC}"

        # Create extraction directory
        mkdir -p "$EXTRACT_PATH"

        # Extract the package (nupkg is a zip file)
        unzip -q "$NUPKG" -d "$EXTRACT_PATH"

        echo -e "${CYAN}Package contents for $PACKAGE_NAME:${NC}"
        echo ""

        # Show key files only
        find "$EXTRACT_PATH" -type f \( -name "*.dll" -o -name "*.xml" -o -name "README.md" -o -name "*.png" \) | while read -r FILE; do
            FILE_SIZE=$(du -k "$FILE" | cut -f1)
            FILE_SIZE_KB=$(echo "scale=2; $FILE_SIZE" | bc)
            echo -e "${GRAY}  $(basename "$FILE") ($FILE_SIZE_KB KB)${NC}"
        done
    fi
done

echo ""
echo -e "${GREEN}======================================${NC}"
echo -e "${GREEN}  Package inspection completed!${NC}"
echo -e "${GREEN}======================================${NC}"
echo ""

# Verification summary
echo -e "${YELLOW}Verification:${NC}"
ALL_PACKAGES_VALID=true

for NUPKG in "$OUTPUT_DIR"/*.nupkg; do
    if [ -f "$NUPKG" ]; then
        PACKAGE_NAME=$(basename "$NUPKG" .nupkg)
        EXTRACT_PATH="$EXTRACT_DIR/$PACKAGE_NAME"

        echo -e "${CYAN}  $PACKAGE_NAME:${NC}"

        # Check for expected files
        EXPECTED_FILES=("README.md" "nuget_logo.png")
        for EXPECTED_FILE in "${EXPECTED_FILES[@]}"; do
            if find "$EXTRACT_PATH" -type f -name "$EXPECTED_FILE" | grep -q .; then
                echo -e "${GREEN}    [OK] $EXPECTED_FILE found${NC}"
            else
                echo -e "${RED}    [MISSING] $EXPECTED_FILE not found!${NC}"
                ALL_PACKAGES_VALID=false
            fi
        done

        # Check for DLL files
        # Extract project name without version (e.g., "Rivulet.Core.1.0.0-local-test" -> "Rivulet.Core")
        PROJECT_NAME=$(echo "$PACKAGE_NAME" | sed -E 's/\.[0-9]+\.[0-9]+\.[0-9]+.*$//')
        DLL_NAME="$PROJECT_NAME.dll"
        DLL_COUNT=$(find "$EXTRACT_PATH" -type f -name "$DLL_NAME" | wc -l)

        if [ "$DLL_COUNT" -gt 0 ]; then
            echo -e "${GREEN}    [OK] $DLL_NAME found in $DLL_COUNT target(s)${NC}"
        else
            echo -e "${RED}    [MISSING] $DLL_NAME not found!${NC}"
            ALL_PACKAGES_VALID=false
        fi
    fi
done

echo ""

if [ "$ALL_PACKAGES_VALID" = true ]; then
    echo -e "${GREEN}All packages valid!${NC}"
else
    echo -e "${YELLOW}Warning: Some packages have missing files.${NC}"
fi

echo ""
echo -e "${YELLOW}Next steps:${NC}"
echo -e "${GRAY}  - Build specific package:  ./NugetPackage.sh <version> Core${NC}"
echo -e "${GRAY}                             ./NugetPackage.sh <version> Diagnostics${NC}"
echo -e "${GRAY}                             ./NugetPackage.sh <version> DiagnosticsOpenTelemetry${NC}"
echo -e "${GRAY}                             ./NugetPackage.sh <version> Testing${NC}"
echo -e "${GRAY}                             ./NugetPackage.sh <version> Hosting${NC}"
echo -e "${GRAY}  - Build all packages:      ./NugetPackage.sh <version> All${NC}"
echo -e "${GRAY}  - Test locally:            dotnet add package Rivulet.Core --source ./test-packages${NC}"
echo ""
