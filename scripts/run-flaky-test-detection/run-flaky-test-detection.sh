#!/bin/bash

# Note: We exit on errors for restore/build, but continue for test failures
# We want to continue testing even when individual test runs fail

# ANSI color codes
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
NC='\033[0m' # No Color

# Navigate to repository root (2 levels up from scripts/run-flaky-test-detection/)
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR/../.."

# Default iterations
ITERATIONS="${1:-20}"

declare -A results

echo -e "${CYAN}Running tests $ITERATIONS times to detect flaky tests...${NC}"
echo ""

# Fail fast for restore/build errors
set -e
dotnet restore
dotnet build -c Release --no-restore
set +e  # But continue on test failures

for ((i = 1; i <= ITERATIONS; i++)); do
    # Calculate percentage
    percent=$((($i * 100) / $ITERATIONS))
    echo -ne "\rRunning Test Iteration: $i of $ITERATIONS ($percent%)"

    # Run tests and capture output (ignore exit code - we check output instead)
    output=$(dotnet test -c Release 2>&1) || true

    # Extract failed test names - xUnit format: "[xUnit.net 00:00:02.19]     TestName [FAIL]"
    # We parse every line looking for [FAIL] markers, no need to check summary first
    while IFS= read -r line; do
        if [[ $line =~ \[xUnit\.net.*\][[:space:]]+(.+)[[:space:]]+\[FAIL\] ]]; then
            testName="${BASH_REMATCH[1]}"
            testName=$(echo "$testName" | xargs) # Trim whitespace

            if [[ -z "${results[$testName]}" ]]; then
                results[$testName]=0
            fi

            ((results[$testName]++))
        fi
    done <<< "$output"

    # Small delay to avoid resource contention
    sleep 0.1
done

echo ""
echo ""

echo -e "${GREEN}========================================${NC}"
echo -e "${GREEN}FLAKY TEST DETECTION RESULTS${NC}"
echo -e "${GREEN}========================================${NC}"
echo ""

if (( ${#results[@]} == 0 )); then
    echo -e "${GREEN}[OK] No flaky tests detected! All tests passed in all $ITERATIONS iterations.${NC}"
else
    echo -e "${YELLOW}[WARNING] FLAKY TESTS DETECTED:${NC}"
    echo ""

    # Sort results by failure count (descending)
    for testName in "${!results[@]}"; do
        echo "$testName ${results[$testName]}"
    done | sort -k2 -rn | while read testName failCount; do
        passCount=$((ITERATIONS - failCount))
        failureRate=$(awk "BEGIN {printf \"%.2f\", ($failCount / $ITERATIONS) * 100}")

        echo -e "${CYAN}Test: $testName${NC}"
        echo -e "  Failures: ${RED}$failCount / $ITERATIONS ($failureRate%)${NC}"
        echo -e "  Passes:   ${GREEN}$passCount / $ITERATIONS${NC}"
        echo ""
    done

    echo -e "${YELLOW}========================================${NC}"
    echo -e "${YELLOW}SUMMARY:${NC}"
    echo -e "${YELLOW}  Total flaky tests: ${#results[@]}${NC}"
    echo -e "${YELLOW}  Total iterations: $ITERATIONS${NC}"
    echo -e "${YELLOW}========================================${NC}"

    # Exit with error code if flaky tests detected (for CI)
    exit 1
fi

echo ""
