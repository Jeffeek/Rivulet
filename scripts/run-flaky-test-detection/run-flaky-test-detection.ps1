param(
    [Parameter(Mandatory=$false)]
    [int]$Iterations = 20
)

# Navigate to repository root (2 levels up from scripts/run-flaky-test-detection/)
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location (Join-Path $ScriptDir "../..")

$results = @{}

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
    $currentDir = Get-Location
    $job = Start-Job -ScriptBlock {
        param($dir)
        Set-Location $dir
        dotnet test -c Release 2>&1 | Out-String
    } -ArgumentList $currentDir

    $completed = Wait-Job -Job $job -Timeout 300 # 5 minutes

    if ($null -eq $completed) {
        Write-Host "[ERROR] Test iteration $i TIMED OUT after 5 minutes - possible deadlock!" -ForegroundColor Red
        Stop-Job -Job $job
        Remove-Job -Job $job

        # Record timeout as a failure
        $timeoutTest = "TIMEOUT_ITERATION_$i"
        if (-not $results.ContainsKey($timeoutTest)) {
            $results[$timeoutTest] = 0
        }
        $results[$timeoutTest]++
        continue
    }

    # Get output from completed job
    $output = Receive-Job -Job $job
    Remove-Job -Job $job

    # Extract failed test names - xUnit format: "[xUnit.net 00:00:02.19]     TestName [FAIL]"
    # We parse every line looking for [FAIL] markers, no need to check summary first
    $failedTests = $output | Select-String -Pattern "\[xUnit\.net.*?\]\s+(.+?)\s+\[FAIL\]" -AllMatches

    foreach ($match in $failedTests.Matches) {
        $testName = $match.Groups[1].Value.Trim()

        if (-not $results.ContainsKey($testName)) {
            $results[$testName] = 0
        }

        $results[$testName]++
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

        Write-Host ""
    }

    Write-Host "========================================" -ForegroundColor Yellow
    Write-Host "SUMMARY:" -ForegroundColor Yellow
    Write-Host "  Total flaky tests: $($results.Count)" -ForegroundColor Yellow
    Write-Host "  Total iterations: $Iterations" -ForegroundColor Yellow
    Write-Host "========================================" -ForegroundColor Yellow

    # Exit with error code if flaky tests detected (for CI)
    exit 1
}

Write-Host ""
