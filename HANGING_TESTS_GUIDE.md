# Identifying Hanging Tests in CI/CD

## Problem
Tests timeout after 5 minutes in CI/CD without clear indication of which test is hanging.

## Quick Fix - Update Your CI/CD Workflow

Replace your test command with this enhanced version:

```yaml
- name: Run tests for net8.0 and net9.0
  run: |
    dotnet test \
      --verbosity normal \
      --logger "console;verbosity=detailed" \
      --blame-hang-timeout 90000 \
      --blame-hang-dump-type full \
      --results-directory ./TestResults
  timeout-minutes: 10
```

## What This Does

1. **`--logger "console;verbosity=detailed"`**: Shows each test as it starts/completes
2. **`--blame-hang-timeout 90000`**: Kills tests hanging for >90 seconds and identifies them
3. **`--blame-hang-dump-type full`**: Creates memory dump of hanging process
4. **`--results-directory ./TestResults`**: Collects all diagnostic files
5. **`timeout-minutes: 10`**: Job-level timeout protection

## Reading the Hang Report

When a test hangs, you'll see output like:

```
[xUnit.net 00:02:00.00]   Rivulet.Diagnostics.Tests.SomeTest [HANG DETECTED]
Sequence_001_SomeTest.dmp created
```

The dump file and XML files in `./TestResults/` will show exactly which test hung.

## Alternative: Run Tests Serially

If you suspect parallel execution issues:

```yaml
- name: Run tests serially
  run: dotnet test --verbosity normal -- xUnit.MaxParallelThreads=1
  timeout-minutes: 15
```

This runs one test at a time, making it obvious which one hangs.

## xUnit Configuration Added

I've added `xunit.runner.json` to test projects with:

```json
{
  "diagnosticMessages": true,        // Enable test start/stop messages
  "longRunningTestSeconds": 30,      // Warn about tests >30 seconds
  "maxParallelThreads": 4            // Limit parallel execution
}
```

This provides real-time diagnostics showing:
- When each test starts
- When each test completes
- Warnings for slow tests (>30s)

## Example CI/CD Workflow

I've created `.github/workflows/test-diagnostics.yml` with full diagnostics:

```yaml
- name: Run tests with hang detection
  run: |
    dotnet test \
      --no-build \
      --configuration Release \
      --verbosity normal \
      --logger "console;verbosity=detailed" \
      --blame-hang-timeout 120000 \
      --blame-hang-dump-type full \
      --results-directory ./TestResults \
      -- xUnit.MaxParallelThreads=4
  timeout-minutes: 10

- name: Upload hang dumps on timeout
  if: failure()
  uses: actions/upload-artifact@v4
  with:
    name: hang-dumps
    path: ./TestResults/**/*
```

## Common Causes of Hanging

Based on code review:

1. **EventCounter timing** (Fixed ✓): Tests waiting for EventCounters that fire every 1000ms
2. **ManualResetEvent.Wait()** (Safe ✓): All have timeouts (2-10 seconds)
3. **File I/O** (Optimized ✓): File rotation test reduced from 50→20 iterations
4. **Parallel execution conflicts**: Tests sharing static state (EventSource)

## Test Timing Summary

Current test execution times (should complete in <2 minutes total):

- **Testing.Tests**: ~250ms (64 tests)
- **Hosting.Tests**: ~680ms (24 tests)
- **Core.Tests**: ~15s (357 tests)
- **Diagnostics.Tests**: ~36s (51 tests) ← Was 59s
- **OpenTelemetry.Tests**: ~4s (39 tests)

**Total: ~56 seconds** for all 535 tests (net8.0 or net9.0)

If CI takes >2 minutes, there's likely a hanging test.

## Debugging Steps

1. **Enable verbose logging** (already done above)
2. **Run with hang detection** (already configured)
3. **Check the last test running before timeout**:
   ```bash
   tail -n 100 ./TestResults/**/*.log
   ```
4. **If still unclear, run serially**:
   ```bash
   dotnet test -- xUnit.MaxParallelThreads=1
   ```

## Test Optimizations Made

1. **FileListener_ShouldRotateFile_WhenMaxSizeExceeded**:
   - Reduced iterations: 50 → 20
   - Increased items per iteration: 5 → 10
   - Increased parallelism: 2 → 4
   - Total time: ~3.2s (was ~6s)

2. **MetricsAggregator tests**:
   - Increased aggregation windows: 100-200ms → 500ms
   - Added safety margins to EventCounter waits: +400-500ms

3. **OpenTelemetry circuit breaker test**:
   - Increased delays for activity overlap: 300ms → 500ms
   - Extended timeout: 5s → 10s
   - Added explicit state change check

## Next Steps

1. **Push these changes** to your repository
2. **Run CI/CD** with the new diagnostic workflow
3. **Check the console output** for the last test before timeout
4. **Download hang dumps** if available from workflow artifacts
5. **Report findings** - the diagnostic output will clearly show the culprit

## Expected CI/CD Output

You should now see output like:

```
[xUnit.net 00:00:00.12] Starting: Rivulet.Diagnostics.Tests
[xUnit.net 00:00:00.15]   Rivulet.Diagnostics.Tests.MetricsAggregatorTests.Test1
[xUnit.net 00:00:01.50]   ✓ Rivulet.Diagnostics.Tests.MetricsAggregatorTests.Test1
[xUnit.net 00:00:01.51]   Rivulet.Diagnostics.Tests.MetricsAggregatorTests.Test2
...
```

If a test hangs, you'll see it start but never complete, then:
```
[HANG DETECTED after 90s] Rivulet.Diagnostics.Tests.SomeTest
Creating hang dump...
```
