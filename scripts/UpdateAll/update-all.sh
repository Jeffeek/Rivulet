#!/bin/bash

# Rivulet Package Management - Update all generated files
# This script regenerates all documentation and configuration files from packages.yml
# See PACKAGE_MANAGEMENT.md for details

set -e

# ANSI color codes
CYAN='\033[0;36m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
NC='\033[0m' # No Color

echo -e "${CYAN}========================================${NC}"
echo -e "${CYAN}Rivulet Package Management${NC}"
echo -e "${CYAN}Updating all generated files...${NC}"
echo -e "${CYAN}========================================${NC}"
echo ""

# Navigate to repository root
# Script is in scripts/UpdateAll/, so go up two levels to reach repo root
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR/../.."

# Check if Python is available
if ! command -v python3 &> /dev/null && ! command -v python &> /dev/null; then
    echo -e "${RED}❌ Error: Python 3 is required but not found${NC}"
    echo "   Please install Python 3.8 or later"
    exit 1
fi

PYTHON_CMD="python3"
if ! command -v python3 &> /dev/null; then
    PYTHON_CMD="python"
fi

# Check if PyYAML is installed
if ! $PYTHON_CMD -c "import yaml" &> /dev/null; then
    echo -e "${YELLOW}⚠️  PyYAML not found. Installing...${NC}"
    $PYTHON_CMD -m pip install --quiet pyyaml || {
        echo -e "${RED}❌ Failed to install PyYAML${NC}"
        echo "   Please run: pip install pyyaml"
        exit 1
    }
    echo -e "${GREEN}✅ PyYAML installed${NC}"
    echo ""
fi

# Run validation first
echo -e "${CYAN}Step 1: Validating package registry...${NC}"
$PYTHON_CMD scripts/package_registry.py || {
    echo ""
    echo -e "${RED}❌ Package registry validation failed${NC}"
    echo "   Please fix the errors in packages.yml"
    exit 1
}
echo ""

# Generate all files
echo -e "${CYAN}Step 2: Generating files...${NC}"
$PYTHON_CMD scripts/generate-all.py --verbose || {
    echo ""
    echo -e "${RED}❌ File generation failed${NC}"
    exit 1
}
echo ""

echo -e "${GREEN}========================================${NC}"
echo -e "${GREEN}✅ All files updated successfully!${NC}"
echo -e "${GREEN}========================================${NC}"
echo ""
echo "Generated files:"
echo "  - README.md (package list)"
echo "  - samples/README.md"
echo "  - docs/ROADMAP.md (or ROADMAP.md)"
echo "  - .github/workflows/release.yml"
echo "  - .github/workflows/nuget-activity-monitor.yml"
echo "  - .github/dependabot.yml"
echo ""
echo "Next steps:"
echo "  1. Review the changes: git diff"
echo "  2. Commit the changes: git add packages.yml README.md samples/README.md ROADMAP.md .github/ && git commit -m 'Update generated files'"
echo ""
