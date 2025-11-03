#!/bin/bash

# ANSI color codes
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
NC='\033[0m' # No Color

iterations=20
declare -A results

echo -e "${CYAN}Running tests $iterations times to detect flaky tests...${NC}"
echo ""

dotnet restore
dotnet build -c Release --no-restore

for ((i = 1; i <= iterations; i++)); do
    # Calculate percentage
    percent=$((($i * 100) / $iterations))
    echo -ne "\rRunning Test Iteration: $i of $iterations ($percent%)"

    output=$(dotnet test -c Release 2>&1)

    # Check if any tests failed
    if [[ $output =~ Failed!\s+-\s+Failed:[[:space:]]+([0-9]+) ]]; then
        failedCount="${BASH_REMATCH[1]}"

        if (( failedCount > 0 )); then
            # Extract failed test names - format: "  Failed TestName [duration]"
            while IFS= read -r line; do
                if [[ $line =~ Failed[[:space:]]+(.+)[[:space:]]+\[ ]]; then
                    testName="${BASH_REMATCH[1]}"
                    testName=$(echo "$testName" | xargs) # Trim whitespace

                    if [[ -z "${results[$testName]}" ]]; then
                        results[$testName]=0
                    fi

                    ((results[$testName]++))
                fi
            done <<< "$output"
        fi
    fi

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
    echo -e "${GREEN}[OK] No flaky tests detected! All tests passed in all $iterations iterations.${NC}"
else
    echo -e "${YELLOW}[WARNING] FLAKY TESTS DETECTED:${NC}"
    echo ""

    # Sort results by failure count (descending)
    for testName in "${!results[@]}"; do
        echo "$testName ${results[$testName]}"
    done | sort -k2 -rn | while read testName failCount; do
        passCount=$((iterations - failCount))
        failureRate=$(awk "BEGIN {printf \"%.2f\", ($failCount / $iterations) * 100}")

        echo -e "${CYAN}Test: $testName${NC}"
        echo -e "  Failures: ${RED}$failCount / $iterations ($failureRate%)${NC}"
        echo -e "  Passes:   ${GREEN}$passCount / $iterations${NC}"
        echo ""
    done

    echo -e "${YELLOW}========================================${NC}"
    echo -e "${YELLOW}SUMMARY:${NC}"
    echo -e "${YELLOW}  Total flaky tests: ${#results[@]}${NC}"
    echo -e "${YELLOW}  Total iterations: $iterations${NC}"
    echo -e "${YELLOW}========================================${NC}"
fi

echo ""
