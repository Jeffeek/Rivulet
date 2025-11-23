param(
    [Parameter(Mandatory=$false)]
    [int]$Iterations = 20
)

function Exit-OrphanedProcesses {
    # Cleanup any remaining test processes that might have been orphaned
    # Target specific test-related processes to avoid killing unrelated dotnet processes
    Write-Host "Cleaning up any orphaned test processes..." -ForegroundColor Gray
    
    # Kill testhost and vstest processes first
    Get-Process testhost -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
    Get-Process vstest.console -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
    
    # Small delay to allow process tree cleanup
    Start-Sleep -Milliseconds 500
    
    # Now kill any orphaned dotnet processes that were spawned by the test jobs
    # These are processes that should have exited when the jobs completed
    $dotnetProcesses = Get-Process dotnet -ErrorAction SilentlyContinue
    if ($dotnetProcesses) {
        Write-Host "  Found $($dotnetProcesses.Count) orphaned dotnet.exe processes, cleaning up..." -ForegroundColor Yellow
        $dotnetProcesses | Stop-Process -Force -ErrorAction SilentlyContinue
    }
}

# Navigate to repository root (2 levels up from scripts/run-flaky-test-detection/)
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location (Join-Path $ScriptDir "../..")

$results = @{}
$errorDetails = @{} # Store first error occurrence for each test

Write-Host "Running tests $Iterations times to detect flaky tests..." -ForegroundColor Cyan
Write-Host ""

# Fail fast for restore/build errors
$ErrorActionPreference = "Stop"
dotnet restore
dotnet build -c Release --no-restore

# But continue on test failures - we want to track flaky tests
$ErrorActionPreference = "Continue"

for ($i = 1; $i -le $Iterations; $i++) {
    $percent = [math]::Round(($i / $Iterations) * 100, 2)
    Write-Host "Running Test Iteration: $i of $Iterations ($percent%)" -ForegroundColor Gray

    # Run tests with 5-minute timeout per iteration to catch hangs
    # IMPORTANT: Pass working directory to job since Start-Job doesn't inherit it
    # FILTER: Exclude integration tests (Testcontainers) - they are deterministic but slow
    $currentDir = Get-Location
    $job = Start-Job -ScriptBlock {
        param($dir)
        Set-Location $dir
        dotnet test -c Release --filter "Category!=Integration" 2>&1 | Out-String
    } -ArgumentList $currentDir

    $completed = Wait-Job -Job $job -Timeout 300 # 5 minutes

    if ($null -eq $completed) {
        Write-Host "[ERROR] Test iteration $i TIMED OUT after 5 minutes - possible deadlock!" -ForegroundColor Red

        # Force stop and remove the job
        Stop-Job -Job $job -Force -ErrorAction SilentlyContinue
        Remove-Job -Job $job -Force -ErrorAction SilentlyContinue

        # Kill any orphaned dotnet processes to prevent resource leaks
        Get-Process dotnet -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue

        # Record timeout as a failure
        $timeoutTest = "TIMEOUT_ITERATION_$i"
        if (-not $results.ContainsKey($timeoutTest)) {
            $results[$timeoutTest] = 0
            $errorDetails[$timeoutTest] = "Test execution timed out after 5 minutes - possible deadlock or hang"
        }
        $results[$timeoutTest]++
        continue
    }

    # Get output from completed job
    $output = Receive-Job -Job $job
    Remove-Job -Job $job -Force

    # Extract failed test names and their error details
    # xUnit format: "[xUnit.net 00:00:02.19]     TestName [FAIL]"
    # Followed by error message and stack trace on subsequent lines
    $lines = $output -split "`n"

    for ($lineIdx = 0; $lineIdx -lt $lines.Length; $lineIdx++) {
        $line = $lines[$lineIdx]

        # Check if this line contains a test failure
        if ($line -match "\[xUnit\.net.*?\]\s+(.+?)\s+\[FAIL\]") {
            $testName = $matches[1].Trim()

            # Track failure count
            if (-not $results.ContainsKey($testName)) {
                $results[$testName] = 0
            }
            $results[$testName]++

            # Capture error details if this is the first occurrence
            if (-not $errorDetails.ContainsKey($testName)) {
                $errorLines = @()

                # Capture subsequent lines until we hit another test result or end
                $lineIdx++
                while ($lineIdx -lt $lines.Length) {
                    $nextLine = $lines[$lineIdx]

                    # Stop if we hit another test result marker
                    if ($nextLine -match "\[xUnit\.net.*?\]\s+.+?\s+\[(FAIL|PASS|SKIP)\]") {
                        $lineIdx-- # Step back so outer loop processes this line
                        break
                    }

                    # Stop if we hit the summary section
                    if ($nextLine -match "^(Failed!|Passed!|\s*Total tests:)") {
                        break
                    }

                    # Capture meaningful error lines (skip empty xUnit prefix lines)
                    $trimmedLine = $nextLine -replace "^\[xUnit\.net.*?\]\s*", ""
                    if ($trimmedLine.Trim()) {
                        $errorLines += $trimmedLine
                    }

                    $lineIdx++

                    # Limit error capture to 30 lines to avoid excessive output
                    if ($errorLines.Count -ge 30) {
                        $errorLines += "  ... (error output truncated)"
                        break
                    }
                }

                if ($errorLines.Count -gt 0) {
                    $errorDetails[$testName] = $errorLines -join "`n"
                } else {
                    $errorDetails[$testName] = "(No error details captured)"
                }
            }
        }
    }

    # Small delay to avoid resource contention
    Start-Sleep -Milliseconds 100
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "FLAKY TEST DETECTION RESULTS" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""

if ($results.Count -eq 0) {
    Write-Host "[OK] No flaky tests detected! All tests passed in all $Iterations iterations." -ForegroundColor Green
} else {
    Write-Host "[WARNING] FLAKY TESTS DETECTED:" -ForegroundColor Yellow
    Write-Host ""

    $sortedResults = $results.GetEnumerator() | Sort-Object -Property Value -Descending

    foreach ($test in $sortedResults) {
        $testName = $test.Key
        $failCount = $test.Value
        $passCount = $Iterations - $failCount
        $failureRate = [math]::Round(($failCount / $Iterations) * 100, 2)

        Write-Host "Test: $testName" -ForegroundColor Cyan
        Write-Host ('  Failures: {0} / {1} ({2}%)' -f $failCount, $Iterations, $failureRate) -ForegroundColor Red
        Write-Host ('  Passes:   {0} / {1}' -f $passCount, $Iterations) -ForegroundColor Green

        # Display error details if available
        if ($errorDetails.ContainsKey($testName)) {
            Write-Host ""
            Write-Host "  Error Details (from first failure):" -ForegroundColor Yellow
            $errorText = $errorDetails[$testName]
            foreach ($errorLine in ($errorText -split "`n")) {
                Write-Host "    $errorLine" -ForegroundColor DarkYellow
            }
        }

        Write-Host ""
    }

    Write-Host "========================================" -ForegroundColor Yellow
    Write-Host "SUMMARY:" -ForegroundColor Yellow
    Write-Host "  Total flaky tests: $($results.Count)" -ForegroundColor Yellow
    Write-Host "  Total iterations: $Iterations" -ForegroundColor Yellow
    Write-Host "========================================" -ForegroundColor Yellow
	
	Exit-OrphanedProcesses

    # Exit with error code if flaky tests detected (for CI)
    exit 1
}

Exit-OrphanedProcesses
Write-Host ""