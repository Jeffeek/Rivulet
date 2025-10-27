# GitHub Actions Workflows Documentation

## Overview

The Rivulet.Core project uses **modular, focused workflows** to handle different aspects of the development lifecycle. Each workflow is designed to be simple, maintainable, and easy to understand.

## Workflow Files

1. **CI Pipeline** (`ci.yml`) - Build and test on every push/PR
2. **CodeQL Analysis** (`codeql.yml`) - Security analysis and code quality
3. **Flaky Test Detection** (`flaky-test-detection.yml`) - Detect unreliable tests
4. **Release Pipeline** (`release.yml`) - Publish NuGet packages on git tags
5. **NuGet Activity Monitor** (`nuget-activity-monitor.yml`) - Prevent package unlisting

---

## 1. CI - Build and Test (`ci.yml`)

### Purpose
Main continuous integration pipeline that validates code quality and functionality on every push and pull request.

### Triggers
- Pull requests to `master` or `release/**` branches
- Pushes to `master` or `release/**` branches

### Jobs

#### Quick Test
- **Matrix**: Ubuntu/Windows × .NET 8.0/9.0 (4 combinations)
- **Duration**: ~2-5 minutes
- **Purpose**: Fast validation across platforms and frameworks

#### Test with Coverage
- **Runs on**: Ubuntu and Windows
- **Uses**: .NET 9.0.x
- **Duration**: ~3-5 minutes
- **Features**:
  - Collects XPlat Code Coverage
  - Uploads to Codecov (Ubuntu only)
  - Target: ≥99% line coverage, ≥95% branch coverage

### Status Badge
```markdown
![CI](https://github.com/Jeffeek/Rivulet/actions/workflows/ci.yml/badge.svg)
```

---

## 2. CodeQL Security Analysis (`codeql.yml`)

### Purpose
Automated security vulnerability and code quality analysis using GitHub's CodeQL engine.

### Triggers
- Pull requests to `master` or `release/**` branches
- Pushes to `master` or `release/**` branches
- Weekly schedule: Monday at 00:00 UTC
- Manual trigger: `workflow_dispatch`

### Features
- Scans for security vulnerabilities
- Checks code quality issues
- Uses `security-and-quality` query suite
- GitHub Security tab integration
- Duration: ~5-10 minutes

### Status Badge
```markdown
![CodeQL](https://github.com/Jeffeek/Rivulet/actions/workflows/codeql.yml/badge.svg)
```

---

## 3. Flaky Test Detection (`flaky-test-detection.yml`)

### Purpose
Detect unreliable tests by running them multiple times across different platforms.

### Triggers
- **Pull requests** to `master` or `release/**` branches when .NET code changes
  - Paths: `**.cs`, `**.csproj`, `tests/**`, `src/**`
  - Runs: 30 iterations (faster feedback)
  - Duration: ~10-15 minutes
- **Monthly schedule**: 1st day at 00:00 UTC
  - Runs: 100 iterations (thorough check)
  - Duration: ~30-40 minutes
- **Manual trigger**: `workflow_dispatch` with configurable iterations
  ```bash
  gh workflow run flaky-test-detection.yml -f iterations=50
  ```

### Features
- Adaptive iteration count based on trigger type
- Runs on Ubuntu and Windows
- Reports failure rates and statistics
- Fails if ANY test is flaky (helps maintain quality)

### Example Output
```
[FAILURE] FLAKY TESTS DETECTED:

Test: SomeTest.SomeMethod
  Failures: 5 / 100 (5.0%)
  Passes:   95 / 100

SUMMARY:
  Total flaky tests: 1
  Total iterations: 100
  Failed iterations: 5
```

---

## 4. Release Pipeline (`release.yml`)

### Purpose
Builds, tests, and publishes NuGet packages only when you explicitly create a git tag.

### Triggers
- Git tags matching pattern `v*` (e.g., `v1.0.0`, `v1.2.3`, `v1.0.0-beta`)

### Workflow Steps
1. Extract version from tag
2. Restore dependencies
3. Build in Release configuration
4. Run tests (5-minute timeout)
5. Pack NuGet package
6. Create GitHub release with release notes
7. Upload artifacts (90-day retention)
8. Publish to NuGet.org

### Duration
~5-10 minutes

### Requirements
- `NUGET_API_KEY` secret must be configured in repository settings

### Status Badge
```markdown
![Release](https://github.com/Jeffeek/Rivulet/actions/workflows/release.yml/badge.svg)
```

---

## 5. NuGet Package Activity Monitor (`nuget-activity-monitor.yml`)

### Purpose
Monitor NuGet package activity to prevent automatic unlisting after 365 days of inactivity.

### Triggers
- Quarterly schedule: 1st day of every 3rd month at 00:00 UTC
- Manual trigger: `workflow_dispatch`

### Features
- Queries NuGet.org API for last publish date
- Creates GitHub issue when package reaches 300+ days of inactivity
- Prevents duplicate issues
- Includes actionable guidance and recommendations
- Duration: ~1-2 minutes

### NuGet Policy
NuGet.org may unlist packages that have:
- No updates for 365 days
- Low download counts

Unlisted packages remain available but are hidden from search results.

---

## Workflow Architecture

### Dependency Graph

```
Pull Request / Push
├── ci.yml (always runs)
│   ├── quick-test (parallel: 4 OS/dotnet combinations)
│   └── test-with-coverage (parallel: 2 OS, after quick-test)
├── codeql.yml (runs independently)
└── flaky-test-detection.yml (when .NET code changes, 30 iterations)

Tag Push (v*)
└── release.yml (publishes to NuGet)

Scheduled / Manual
├── flaky-test-detection.yml (monthly, 100 iterations / on-demand with custom count)
├── codeql.yml (weekly)
└── nuget-activity-monitor.yml (quarterly)
```

### Design Principles
1. **Separation of Concerns**: Each workflow has a single, clear purpose
2. **Modularity**: Workflows run independently without dependencies
3. **Maintainability**: Simple, easy to understand and modify
4. **Efficiency**: Parallel execution where possible
5. **Observability**: Clear naming, detailed logs, status reporting

---

## Creating a Release

### Step-by-Step

1. **Ensure all CI tests pass** on your branch

2. **Update version** and commit changes:
   ```bash
   git checkout -b release/1.2.3
   # Update CHANGELOG, version references, etc.
   git add .
   git commit -m "Prepare release 1.2.3"
   git push origin release/1.2.3
   ```

3. **Create and push a git tag**:
   ```bash
   # Create annotated tag
   git tag -a v1.2.3 -m "Release version 1.2.3"

   # Push tag to GitHub (triggers release.yml)
   git push origin v1.2.3
   ```

4. **Monitor the workflow**:
   - Go to Actions tab → "Release Package" workflow
   - Wait for completion (~5-10 minutes)

5. **Verify the release**:
   - Check the Releases page on GitHub
   - Verify package is published to NuGet.org
   - Test package installation: `dotnet add package Rivulet.Core --version 1.2.3`

---

## Configuration

### Required Secrets

| Secret | Purpose | Required For |
|--------|---------|--------------|
| `CODECOV_TOKEN` | Upload coverage reports | `ci.yml` |
| `NUGET_API_KEY` | Publish to NuGet.org | `release.yml` |
| `GITHUB_TOKEN` | Built-in, auto-provided | All workflows |

**Setting secrets**:
1. Go to repository Settings → Secrets and variables → Actions
2. Click "New repository secret"
3. Add the secret name and value

### Workflow Permissions

Each workflow uses least-privilege permissions:

| Workflow | Permissions |
|----------|------------|
| `ci.yml` | `contents: read` |
| `codeql.yml` | `security-events: write`, `actions: read`, `contents: read` |
| `flaky-test-detection.yml` | `contents: read` |
| `release.yml` | `contents: write`, `packages: write` |
| `nuget-activity-monitor.yml` | `issues: write`, `contents: read` |

---

## Manual Workflow Triggers

Most workflows run automatically, but can also be triggered manually using GitHub CLI:

```bash
# Run flaky test detection (uses 100 iterations by default)
gh workflow run flaky-test-detection.yml

# Run with custom iteration count (useful for testing)
gh workflow run flaky-test-detection.yml -f iterations=50

# Run CodeQL analysis on-demand
gh workflow run codeql.yml

# Check NuGet package activity
gh workflow run nuget-activity-monitor.yml
```

**Note**: Flaky test detection automatically runs on PRs when .NET code changes (using 30 iterations for faster feedback).

---

## Local Testing

### Run CI Tests Locally

```bash
# Quick test (single iteration)
dotnet test -c Release --verbosity normal

# With coverage
dotnet test -c Release --collect:"XPlat Code Coverage" --results-directory ./coverage

# View coverage report
reportgenerator -reports:"./coverage/**/coverage.cobertura.xml" -targetdir:"./coverage/report" -reporttypes:Html
```

### Package Testing

```bash
# Build and pack locally
dotnet build -c Release
dotnet pack src/Rivulet.Core/Rivulet.Core.csproj -c Release --output ./packages -p:PackageVersion=1.0.0-local

# Test package installation
dotnet new console -n TestApp
cd TestApp
dotnet add package Rivulet.Core --source ../packages --version 1.0.0-local
```

---

## Troubleshooting

### CI Failures

**Problem**: Tests fail on specific OS or .NET version
- Check matrix logs for the specific combination
- Reproduce locally: `dotnet test -f net9.0` or `dotnet test -f net8.0`

**Problem**: Coverage upload fails
- Verify `CODECOV_TOKEN` is correctly set in repository secrets
- Check Codecov service status

**Problem**: Workflow doesn't trigger on PR
- Ensure PR targets `master` or `release/**` branches
- Check workflow syntax with `actionlint`

**Problem**: Flaky test detection takes too long on PR
- PR runs use 30 iterations by default (10-15 minutes)
- Consider if the test is genuinely flaky and needs fixing
- Monthly scheduled runs use 100 iterations for thorough checking

### CodeQL Failures

**Problem**: CodeQL analysis times out
- CodeQL has 6-hour timeout, usually completes in 5-10 minutes
- Check for syntax errors in workflow file

**Problem**: False positive security alerts
- Review findings in Security tab
- Dismiss with justification if not applicable

### Release Failures

**Problem**: Tag already exists
```bash
# Delete local and remote tag
git tag -d v1.0.0
git push origin :refs/tags/v1.0.0

# Create new tag
git tag -a v1.0.0 -m "Release 1.0.0"
git push origin v1.0.0
```

**Problem**: NuGet push fails with 409 Conflict
- Version already exists on NuGet.org
- Increment version number and create new tag

**Problem**: Release workflow doesn't trigger
- Ensure tag format is `v*` (e.g., `v1.0.0`, not `1.0.0`)
- Verify workflow file exists and has no syntax errors
- Check Actions tab for error messages

### Flaky Test Detection

**Problem**: All tests fail during detection run
- Check for environmental issues (memory, disk space)
- Increase timeout value
- Review system logs

**Problem**: High failure rate (>10%)
- Likely indicates a real bug, not a flaky test
- Fix the underlying issue before continuing

---

## Best Practices

### For Contributors
1. Run tests locally before pushing
2. Maintain ≥99% line coverage, ≥95% branch coverage
3. Review CodeQL findings in Security tab
4. Wait for CI to pass before merging PRs

### For Maintainers
1. Use `./Release.ps1` script for consistent releases
2. Review quarterly NuGet activity monitor issues
3. Check monthly flaky test detection results
4. Merge Dependabot PRs promptly
5. Monitor workflow run times and optimize if needed

### For Workflow Modifications
1. Test changes in a fork first
2. Update this README when modifying workflows
3. Maintain backwards compatibility
4. Optimize for parallel execution where possible
5. Use least-privilege permissions

---

## Monitoring and Observability

### GitHub Actions Tab
- View all workflow runs and status
- Filter by workflow, branch, or status
- Download logs and artifacts (90-day retention)

### GitHub Security Tab
- View CodeQL security findings
- Review Dependabot alerts
- Track security advisories

### Codecov Dashboard
- View coverage trends: https://codecov.io/gh/Jeffeek/Rivulet
- Compare coverage across PRs
- Track coverage changes over time

### NuGet.org Dashboard
- Monitor downloads: https://www.nuget.org/packages/Rivulet.Core/
- Check package listing status
- View package statistics

---

## Additional Resources

- [GitHub Actions Documentation](https://docs.github.com/en/actions)
- [CodeQL Documentation](https://codeql.github.com/docs/)
- [Codecov Documentation](https://docs.codecov.com/)
- [NuGet Publishing Guide](https://learn.microsoft.com/en-us/nuget/nuget-org/publish-a-package)
- [Semantic Versioning](https://semver.org/)
- [Repository README](../../README.md)
- [Release Guide](../../RELEASE_GUIDE.md)
- [Dependabot Configuration](../dependabot.yml)

---

**Last Updated**: 2025-10-27
**Maintained by**: @Jeffeek
**Questions?**: Open an issue or discussion on GitHub
