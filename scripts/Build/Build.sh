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
CONFIGURATION="${1:-Debug}"
SKIP_TESTS=false

# Parse command line arguments
for arg in "$@"; do
    case "$arg" in
        Debug|Release)
            CONFIGURATION="$arg"
            ;;
        --skip-tests)
            SKIP_TESTS=true
            ;;
        *)
            echo -e "${RED}Error: Unknown argument '$arg'${NC}"
            echo -e "${YELLOW}Usage: ./Build.sh [Debug|Release] [--skip-tests]${NC}"
            echo -e "${YELLOW}Examples:${NC}"
            echo -e "${GRAY}  ./Build.sh                     # Debug build with tests${NC}"
            echo -e "${GRAY}  ./Build.sh Release             # Release build with tests${NC}"
            echo -e "${GRAY}  ./Build.sh --skip-tests        # Debug build without tests${NC}"
            echo -e "${GRAY}  ./Build.sh Release --skip-tests# Release build without tests${NC}"
            exit 1
            ;;
    esac
done

# Validate configuration
if [ "$CONFIGURATION" != "Debug" ] && [ "$CONFIGURATION" != "Release" ]; then
    echo -e "${RED}Error: Configuration must be 'Debug' or 'Release'${NC}"
    exit 1
fi

# Navigate to repository root (2 levels up from scripts/Build/)
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR/../.."

echo -e "${CYAN}======================================${NC}"
echo -e "${CYAN}  Rivulet Build Script${NC}"
echo -e "${CYAN}======================================${NC}"
echo ""

echo -e "${GREEN}Configuration: $CONFIGURATION${NC}"
echo -e "${GREEN}Skip Tests:    $SKIP_TESTS${NC}"
echo ""

# Step 1: Clean
echo -e "${YELLOW}======================================${NC}"
echo -e "${YELLOW}  Step 1/4: Cleaning solution...${NC}"
echo -e "${YELLOW}======================================${NC}"
echo ""

if ! dotnet clean -c "$CONFIGURATION"; then
    echo ""
    echo -e "${RED}Error: Clean failed!${NC}"
    exit 1
fi

echo ""
echo -e "${GREEN}Clean completed successfully!${NC}"
echo ""

# Step 2: Restore
echo -e "${YELLOW}======================================${NC}"
echo -e "${YELLOW}  Step 2/4: Restoring dependencies...${NC}"
echo -e "${YELLOW}======================================${NC}"
echo ""

if ! dotnet restore; then
    echo ""
    echo -e "${RED}Error: Restore failed!${NC}"
    exit 1
fi

echo ""
echo -e "${GREEN}Restore completed successfully!${NC}"
echo ""

# Step 3: Build
echo -e "${YELLOW}======================================${NC}"
echo -e "${YELLOW}  Step 3/4: Building solution...${NC}"
echo -e "${YELLOW}======================================${NC}"
echo ""

if ! dotnet build -c "$CONFIGURATION" --no-restore; then
    echo ""
    echo -e "${RED}Error: Build failed!${NC}"
    exit 1
fi

echo ""
echo -e "${GREEN}Build completed successfully!${NC}"
echo ""

# Step 4: Test (optional)
if [ "$SKIP_TESTS" = false ]; then
    echo -e "${YELLOW}======================================${NC}"
    echo -e "${YELLOW}  Step 4/4: Running tests...${NC}"
    echo -e "${YELLOW}======================================${NC}"
    echo ""

    if ! dotnet test -c "$CONFIGURATION" --no-build --verbosity normal; then
        echo ""
        echo -e "${RED}Error: Tests failed!${NC}"
        exit 1
    fi

    echo ""
    echo -e "${GREEN}Tests completed successfully!${NC}"
    echo ""
else
    echo -e "${GRAY}======================================${NC}"
    echo -e "${GRAY}  Step 4/4: Tests skipped${NC}"
    echo -e "${GRAY}======================================${NC}"
    echo ""
fi

# Summary
echo -e "${GREEN}======================================${NC}"
echo -e "${GREEN}  Build process completed!${NC}"
echo -e "${GREEN}======================================${NC}"
echo ""

echo -e "${CYAN}Configuration: $CONFIGURATION${NC}"
echo -e "${CYAN}Tests run:     $([ "$SKIP_TESTS" = false ] && echo 'true' || echo 'false')${NC}"
echo ""

echo -e "${YELLOW}Next steps:${NC}"
echo -e "${GRAY}  - Run tests:       ./Build.sh${NC}"
echo -e "${GRAY}  - Build release:   ./Build.sh Release${NC}"
echo -e "${GRAY}  - Skip tests:      ./Build.sh --skip-tests${NC}"
echo -e "${GRAY}  - Create package:  ./NugetPackage.sh${NC}"
echo ""
