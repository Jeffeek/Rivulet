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

# Check if Python is available and intelligently select the best one
PYTHON_CMD=""
PYTHON3_AVAILABLE=false
PYTHON_AVAILABLE=false
PYTHON3_HAS_YAML=false
PYTHON_HAS_YAML=false

# Check python3
if command -v python3 &> /dev/null; then
    PYTHON3_AVAILABLE=true
    if python3 -c "import yaml" &> /dev/null; then
        PYTHON3_HAS_YAML=true
    fi
fi

# Check python
if command -v python &> /dev/null; then
    PYTHON_AVAILABLE=true
    if python -c "import yaml" &> /dev/null; then
        PYTHON_HAS_YAML=true
    fi
fi

# Exit if no Python found
if [ "$PYTHON3_AVAILABLE" = false ] && [ "$PYTHON_AVAILABLE" = false ]; then
    echo -e "${RED}[ERR] Error: Python 3 is required but not found${NC}"
    echo "   Please install Python 3.8 or later"
    exit 1
fi

# Prefer the Python command that already has PyYAML installed
if [ "$PYTHON3_HAS_YAML" = true ]; then
    PYTHON_CMD="python3"
elif [ "$PYTHON_HAS_YAML" = true ]; then
    PYTHON_CMD="python"
elif [ "$PYTHON3_AVAILABLE" = true ]; then
    PYTHON_CMD="python3"
else
    PYTHON_CMD="python"
fi

# Install PyYAML if needed
if ! $PYTHON_CMD -c "import yaml" &> /dev/null; then
    echo -e "${YELLOW}[WARN]  PyYAML not found. Installing...${NC}"
    $PYTHON_CMD -m pip install --user --quiet pyyaml || {
        echo -e "${RED}[ERR] Failed to install PyYAML${NC}"
        echo "   Please run: $PYTHON_CMD -m pip install pyyaml"
        exit 1
    }
    echo -e "${GREEN}[OK] PyYAML installed${NC}"
    echo ""
fi

# Run validation first
echo -e "${CYAN}Step 1: Validating package registry...${NC}"
$PYTHON_CMD scripts/UpdateAll/package_registry.py || {
    echo ""
    echo -e "${RED}[ERR] Package registry validation failed${NC}"
    echo "   Please fix the errors in packages.yml"
    exit 1
}
echo ""

# Generate all files
echo -e "${CYAN}Step 2: Generating files...${NC}"
$PYTHON_CMD scripts/UpdateAll/generate-all.py --verbose || {
    echo ""
    echo -e "${RED}[ERR] File generation failed${NC}"
    exit 1
}
echo ""

echo -e "${GREEN}========================================${NC}"
echo -e "${GREEN}[WARN] All files updated successfully!${NC}"
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
