#!/bin/bash

# Note: We exit on errors for restore/build, but continue for test failures
# We want to continue testing even when individual test runs fail

function exit_orphaned_processes {
    # Cleanup any remaining test processes that might have been orphaned
    echo -e "${DARKYELLOW}Cleaning up any orphaned test processes...${NC}"

    # Kill testhost and vstest processes first
    pkill -9 testhost 2>/dev/null || true
    pkill -9 vstest.console 2>/dev/null || true

    # Small delay to allow process tree cleanup
    sleep 0.5

    # Count and kill any orphaned dotnet processes
    dotnet_count=$(pgrep -c dotnet 2>/dev/null || echo "0")
    dotnet_count=$(echo "$dotnet_count" | tr -d '[:space:]')  # Remove all whitespace
    if [[ "$dotnet_count" =~ ^[0-9]+$ ]] && [[ $dotnet_count -gt 0 ]]; then
        echo -e "  ${YELLOW}Found $dotnet_count orphaned dotnet processes, cleaning up...${NC}"
        pkill -9 dotnet 2>/dev/null || true
    fi
}

# ANSI color codes
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
DARKYELLOW='\033[0;33m'
GRAY='\033[0;90m'
NC='\033[0m' # No Color

# Navigate to repository root (2 levels up from scripts/run-flaky-test-detection/)
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR/../.."

# Parse command line arguments
ITERATIONS=20
SKIP_RESTORE=false
SKIP_BUILD=false

# Process arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        --skip-restore)
            SKIP_RESTORE=true
            shift
            ;;
        --skip-build)
            SKIP_BUILD=true
            shift
            ;;
        [0-9]*)
            ITERATIONS=$1
            shift
            ;;
        *)
            echo -e "${RED}Unknown argument: $1${NC}"
            echo "Usage: $0 [iterations] [--skip-restore] [--skip-build]"
            echo "Example: $0 50 --skip-restore --skip-build"
            exit 1
            ;;
    esac
done

declare -A results
declare -A errorDetails # Store first error occurrence for each test

echo -e "${CYAN}Running tests $ITERATIONS times to detect flaky tests...${NC}"
echo ""

# Fail fast for restore/build errors
set -e

# Restore dependencies (unless skipped - useful when using pipeline cache)
if [ "$SKIP_RESTORE" = false ]; then
    echo -e "${GRAY}Restoring NuGet packages...${NC}"
    dotnet restore Rivulet.slnx
else
    echo -e "${GRAY}Skipping restore (using cached packages)${NC}"
fi

# Build solution (unless skipped - useful when using pipeline cache)
if [ "$SKIP_BUILD" = false ]; then
    echo -e "${GRAY}Building solution in Release mode...${NC}"
    if [ "$SKIP_RESTORE" = true ]; then
        dotnet build Rivulet.slnx -c Release
    else
        dotnet build Rivulet.slnx -c Release --no-restore
    fi
else
    echo -e "${GRAY}Skipping build (using cached binaries)${NC}"
fi

echo ""

set +e  # But continue on test failures

for ((i = 1; i <= ITERATIONS; i++)); do
    # Calculate percentage
    percent=$((($i * 100) / $ITERATIONS))
    echo -ne "\rRunning Test Iteration: $i of $ITERATIONS ($percent%)"

    # Run tests with 5-minute timeout per iteration to catch hangs
    # timeout returns 124 if command times out
    # FILTER: Exclude integration tests (Testcontainers) - they are deterministic but slow
    output=$(timeout 300 dotnet test -c Release --filter "Category!=Integration" 2>&1) || test_exit=$?

    # Check if timeout occurred (exit code 124)
    if [[ ${test_exit:-0} -eq 124 ]]; then
        echo ""
        echo -e "${RED}[ERROR] Test iteration $i TIMED OUT after 5 minutes - possible deadlock!${NC}"

        # Record timeout as a failure
        timeoutTest="TIMEOUT_ITERATION_$i"
        if [[ -z "${results[$timeoutTest]}" ]]; then
            results[$timeoutTest]=0
            errorDetails[$timeoutTest]="Test execution timed out after 5 minutes - possible deadlock or hang"
        fi
        ((results[$timeoutTest]++))
        continue
    fi

    # Extract failed test names and their error details
    # xUnit format: "[xUnit.net 00:00:02.19]     TestName [FAIL]"
    # Followed by error message and stack trace on subsequent lines
    IFS=$'\n' read -d '' -r -a lines <<< "$output" || true

    lineIdx=0
    while [[ $lineIdx -lt ${#lines[@]} ]]; do
        line="${lines[$lineIdx]}"

        # Check if this line contains a test failure
        if [[ $line =~ \[xUnit\.net.*\][[:space:]]+(.+)[[:space:]]+\[FAIL\] ]]; then
            testName="${BASH_REMATCH[1]}"
            testName=$(echo "$testName" | xargs) # Trim whitespace

            # Track failure count
            if [[ -z "${results[$testName]}" ]]; then
                results[$testName]=0
            fi
            ((results[$testName]++))

            # Capture error details if this is the first occurrence
            if [[ -z "${errorDetails[$testName]}" ]]; then
                errorLines=()

                # Capture subsequent lines until we hit another test result or end
                ((lineIdx++))
                errorLineCount=0
                while [[ $lineIdx -lt ${#lines[@]} && $errorLineCount -lt 30 ]]; do
                    nextLine="${lines[$lineIdx]}"

                    # Stop if we hit another test result marker
                    if [[ $nextLine =~ \[xUnit\.net.*\][[:space:]]+.+[[:space:]]+\[(FAIL|PASS|SKIP)\] ]]; then
                        ((lineIdx--)) # Step back so outer loop processes this line
                        break
                    fi

                    # Stop if we hit the summary section
                    if [[ $nextLine =~ ^(Failed!|Passed!|[[:space:]]*Total\ tests:) ]]; then
                        break
                    fi

                    # Capture meaningful error lines (remove xUnit prefix)
                    trimmedLine=$(echo "$nextLine" | sed -E 's/^\[xUnit\.net[^]]*\][[:space:]]*//')
                    if [[ -n "${trimmedLine// /}" ]]; then
                        errorLines+=("$trimmedLine")
                        ((errorLineCount++))
                    fi

                    ((lineIdx++))
                done

                if [[ $errorLineCount -ge 30 ]]; then
                    errorLines+=("  ... (error output truncated)")
                fi

                if [[ ${#errorLines[@]} -gt 0 ]]; then
                    # Join array with newlines
                    errorDetails[$testName]=$(IFS=$'\n'; echo "${errorLines[*]}")
                else
                    errorDetails[$testName]="(No error details captured)"
                fi
            fi
        fi

        ((lineIdx++))
    done

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
    # Use tab as delimiter to avoid issues with spaces in test names
    for testName in "${!results[@]}"; do
        printf "%s\t%s\n" "$testName" "${results[$testName]}"
    done | sort -t$'\t' -k2 -rn | while IFS=$'\t' read -r testName failCount; do

        passCount=$((ITERATIONS - failCount))
        failureRate=$(awk "BEGIN {printf \"%.2f\", ($failCount / $ITERATIONS) * 100}")

        echo -e "${CYAN}Test: $testName${NC}"
        echo -e "  Failures: ${RED}$failCount / $ITERATIONS ($failureRate%)${NC}"
        echo -e "  Passes:   ${GREEN}$passCount / $ITERATIONS${NC}"

        # Display error details if available
        if [[ -n "${errorDetails[$testName]}" ]]; then
            echo ""
            echo -e "  ${YELLOW}Error Details (from first failure):${NC}"
            while IFS= read -r errorLine; do
                echo -e "    ${DARKYELLOW}$errorLine${NC}"
            done <<< "${errorDetails[$testName]}"
        fi

        echo ""
    done

    echo -e "${YELLOW}========================================${NC}"
    echo -e "${YELLOW}SUMMARY:${NC}"
    echo -e "${YELLOW}  Total flaky tests: ${#results[@]}${NC}"
    echo -e "${YELLOW}  Total iterations: $ITERATIONS${NC}"
    echo -e "${YELLOW}========================================${NC}"

    exit_orphaned_processes

    # Exit with error code if flaky tests detected (for CI)
    exit 1
fi

exit_orphaned_processes
echo ""
