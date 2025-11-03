param(
    [Parameter(Mandatory=$false)]
    [int]$Iterations = 20
)

$ErrorActionPreference = "Stop"

$results = @{}

Write-Host "Running tests $Iterations times to detect flaky tests..." -ForegroundColor Cyan
Write-Host ""

dotnet restore
dotnet build -c Release --no-restore

for ($i = 1; $i -le $Iterations; $i++) {
    Write-Progress -Activity "Running Test Iteration" -Status "$i of $Iterations" -PercentComplete (($i / $Iterations) * 100)

    $output = dotnet test -c Release | Out-String

    # Check if any tests failed
    if ($output -match "Failed!\s+-\s+Failed:\s+(\d+)") {
        $failedCount = [int]$matches[1]

        if ($failedCount -gt 0) {
            # Extract failed test names - format: "  Failed TestName [duration]"
            $failedTests = $output | Select-String -Pattern "\s+Failed\s+(.*)\s+\[" -AllMatches

            foreach ($match in $failedTests.Matches) {
                $testName = $match.Groups[1].Value.Trim()

                if (-not $results.ContainsKey($testName)) {
                    $results[$testName] = 0
                }

                $results[$testName]++
            }
        }
    }

    # Small delay to avoid resource contention
    Start-Sleep -Milliseconds 100
}

Write-Progress -Activity "Running Test Iteration" -Completed

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
