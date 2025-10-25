# CI/CD Pipeline Documentation

## Overview

The Rivulet project uses **two separate workflows** to handle different aspects of the development lifecycle:

1. **CI Pipeline** (`github-workflow.yml`) - Runs on every push/PR for quality assurance
2. **Release Pipeline** (`release.yml`) - Runs **only on git tags** to publish packages

---

## CI Pipeline (`github-workflow.yml`)

### Purpose
Validates code quality, functionality, and cross-platform compatibility for **every push and pull request**.

### Architecture

```
quick-test (Windows + Linux, .NET 8.0 + 9.0)
    ↓
    ├─→ test-with-coverage (Windows + Linux)
    └─→ flaky-test-detection (Windows + Linux, 200 iterations)
```

**Duration**: ~30-35 minutes

**Triggers**:
- Push to `master` branche
- Pull requests to `master` branche

---

## Release Pipeline (`release.yml`)

### Purpose
Builds, tests, and publishes NuGet packages **only when you explicitly create a git tag**.

### Architecture

```
git tag v1.0.0
    ↓
build → test → pack → create GitHub release → upload artifacts
```

**Duration**: ~3-5 minutes

**Triggers**:
- Git tags matching pattern `v*` (e.g., `v1.0.0`, `v2.1.3`, `v1.0.0-beta`)

---

## Creating a Release

### Step-by-Step

1. **Ensure all CI tests pass** on your branch

2. **Commit your changes** to the release branch:
   ```bash
   git checkout -b release/1.2.3
   git add .
   git commit -m "Prepare release 1.2.3"
   git push origin release/1.2.3
   ```

3. **Create and push a git tag**:
   ```bash
   # Create annotated tag
   git tag -a v1.2.3 -m "Release version 1.2.3"

   # Push tag to GitHub (this triggers the release workflow)
   git push origin v1.2.3
   ```

4. **Monitor the workflow**:
   - Go to GitHub Actions tab
   - Watch the "Release Package" workflow
   - Wait for completion (~3-5 minutes)

5. **Verify the release**:
   - Check the "Releases" page on GitHub
   - Download and inspect the `.nupkg` file if needed
   - Verify the version number is correct

6. **Publish to NuGet.org** (when ready):
   - Add `NUGET_API_KEY` to repository secrets
   - Uncomment the publish step in `release.yml`
   - Re-run the workflow or create a new tag

---

## Branch Strategy

### Recommended Git Flow

```
master (main development)
    ↓
    ├─→ feature/xyz → merge to master
    ├─→ bugfix/abc → merge to master
    └─→ release/{major}.{minor}.{patch} → tag v{version} → merge to master
```

**Branches**:
- `master` - Main development branch, always stable
- `feature/*` - New features
- `bugfix/*` - Bug fixes
- `release/{major}.{minor}.{patch}` - Release preparation

**Workflow**:
1. Develop on `master` or feature branches
2. Create `release/1.2.3` branch when ready for release
3. Finalize release (update changelog, version files, etc.)
4. Create and push tag `v1.2.3` on the release branch
5. Release workflow runs automatically
6. Merge release branch back to `master`

---

## Testing Strategy

### CI Jobs

#### 1. **Quick Test** (Matrix: 4 combinations)

**Purpose**: Fast sanity check

**Runs on**: Windows + Ubuntu
**Tests against**: .NET 8.0.x + 9.0.x
**Duration**: ~2-3 minutes

**Features**:
- ✅ Multi-platform validation (2 OS)
- ✅ Multi-framework validation (2 .NET versions)
- ✅ Parallel execution (4 combinations)
- ✅ 5-minute timeout

---

#### 2. **Test with Coverage** (Matrix: 2 platforms)

**Purpose**: Generate code coverage reports

**Runs on**: Windows + Ubuntu
**Tests against**: .NET 9.0.x
**Duration**: ~2-3 minutes

**Features**:
- ✅ XPlat Code Coverage collection
- ✅ Codecov integration
- ✅ Coverage: **99.5% line, 95.65% branch**

---

#### 3. **Flaky Test Detection** (Matrix: 2 platforms)

**Purpose**: Detect race conditions through repeated execution

**Runs on**: Windows (PowerShell) + Ubuntu (Bash)
**Tests against**: .NET 9.0.x
**Iterations**: **200 per platform** (400 total)
**Duration**: ~20-25 minutes per platform

**Why 200 iterations?**
- Catches timing-dependent bugs
- Validates thread safety
- Ensures cross-platform consistency
- Statistical significance (99.5% pass rate still considered flaky)

**Example output** (if flaky test found):
```
[FAILURE] FLAKY TESTS DETECTED:

Test: SomeTest.SomeMethod
  Failures: 5 / 200 (2.5%)
  Passes:   195 / 200

SUMMARY:
  Total flaky tests: 1
  Total iterations: 200
  Failed iterations: 5
```

**The build FAILS if ANY test is flaky!**

---

## Release Workflow Details

### What Happens When You Push a Tag

1. **Checkout** - Fetches full git history
2. **Extract Version** - Parses version from tag (removes `v` prefix)
3. **Build** - Builds in Release configuration
4. **Test** - Runs all tests once (5-minute timeout)
5. **Pack** - Creates `.nupkg` with version from tag
6. **Create GitHub Release** - Attaches packages, generates release notes
7. **Upload Artifacts** - Stores packages (90-day retention)
8. **Publish to NuGet.org** (optional, commented out by default)

### Version Naming Convention

Follow [Semantic Versioning](https://semver.org/):

| Tag | Version | Type |
|-----|---------|------|
| `v1.0.0` | 1.0.0 | Stable release |
| `v1.2.3` | 1.2.3 | Stable release |
| `v2.0.0-beta` | 2.0.0-beta | Prerelease (beta) |
| `v2.0.0-rc.1` | 2.0.0-rc.1` | Release candidate |
| `v1.0.0-alpha.1` | 1.0.0-alpha.1 | Alpha release |

**Important**:
- Always use `v` prefix for tags
- Prerelease versions automatically marked as prerelease on GitHub
- Use branch naming: `release/{version}` for release branches

---

## Local Testing

### CI Tests

```bash
# Quick test (single iteration)
dotnet test -c Release

# With coverage
dotnet test -c Release --collect:"XPlat Code Coverage"

# Flaky test detection (Windows - 200 iterations)
# Edit run-flaky-test-detection.ps1 and set $iterations = 200
powershell -ExecutionPolicy Bypass -File tests/Rivulet.Core.Tests/run-flaky-test-detection.ps1
```

### Package Testing

```bash
# Build and pack
dotnet build -c Release
dotnet pack -c Release --output ./local-packages -p:PackageVersion=1.0.0-local

# Inspect package contents
unzip -l ./local-packages/Rivulet.Core.1.0.0-local.nupkg

# Test in a local project
dotnet add package Rivulet.Core --source ./local-packages --version 1.0.0-local
```

---

## Configuration

### Required Secrets (for NuGet.org publishing)

| Secret Name | Purpose | How to Obtain |
|-------------|---------|---------------|
| `NUGET_API_KEY` | Publishes to NuGet.org | https://www.nuget.org/account/apikeys |

**Setting secrets**:
1. Go to repository Settings → Secrets and variables → Actions
2. Click "New repository secret"
3. Name: `NUGET_API_KEY`
4. Value: Your NuGet API key
5. Click "Add secret"

### Optional Secrets (for coverage)

| Secret Name | Purpose | Required |
|-------------|---------|----------|
| `CODECOV_TOKEN` | Uploads to Codecov | Only for private repos |

---

## Troubleshooting

### Tag Already Exists

```bash
# Delete local tag
git tag -d v1.0.0

# Delete remote tag
git push origin :refs/tags/v1.0.0

# Create new tag
git tag -a v1.0.0 -m "Release 1.0.0"
git push origin v1.0.0
```

### Release Workflow Not Triggering

Check:
- Tag was pushed to GitHub: `git push origin v1.0.0`
- Tag name matches pattern `v*`
- Workflow file exists: `.github/workflows/release.yml`
- No YAML syntax errors

### Wrong Version in Package

The version comes from the git tag:
- Ensure tag is on correct commit
- Tag must match pattern `v*`
- No typos in tag name

### Flaky Test Detected

1. **Check failure rate**: >10% = bug, <5% = race condition
2. **Analyze test code**: Look for missing `await`, improper sync, timing assumptions
3. **Run locally**: `dotnet test --filter "FullyQualifiedName~YourTest"`
4. **Fix**: Add proper synchronization, use `Task.Delay`, ensure cleanup in `finally`

---

## Performance Characteristics

### CI Pipeline

| Job | Parallelization | Duration |
|-----|-----------------|----------|
| Quick Test | 4 parallel (2 OS × 2 .NET) | ~3 min |
| Coverage Test | 2 parallel (2 OS) | ~3 min (parallel) |
| Flaky Detection | 2 parallel (2 OS) | ~25 min (parallel) |

**Total**: ~30-35 minutes

### Release Pipeline

| Step | Duration |
|------|----------|
| Build + Test + Pack | ~2-3 min |
| Create Release | ~30 sec |

**Total**: ~3-5 minutes

---

## Quick Reference Checklist

### Creating a Release

- [ ] All CI tests pass on target branch
- [ ] Version number decided (follow SemVer)
- [ ] Changelog updated
- [ ] Create release branch: `release/1.2.3`
- [ ] Commit final changes
- [ ] Create tag: `git tag -a v1.2.3 -m "Release 1.2.3"`
- [ ] Push tag: `git push origin v1.2.3`
- [ ] Monitor Actions tab
- [ ] Verify GitHub release created
- [ ] Verify package artifacts attached
- [ ] (Optional) Publish to NuGet.org
- [ ] Merge release branch to master
- [ ] Announce release!

---

## Questions?

For questions about the CI/CD pipeline:
1. Check this documentation
2. Review workflow files:
   - `.github/workflows/github-workflow.yml` (CI)
   - `.github/workflows/release.yml` (Releases)
3. Open an issue on GitHub
