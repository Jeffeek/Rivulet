# Release Guide: Publishing Rivulet v1.0.0 to NuGet

This guide walks you through creating and publishing your first release of Rivulet to NuGet.org.

---

## Prerequisites Checklist

Before starting the release process, ensure:

- [ ] All CI tests pass on `master` branch
- [ ] Code coverage is at expected (95%>=) level (currently ![Codecov (with branch)](https://img.shields.io/codecov/c/github/Jeffeek/Rivulet/master?style=flat&label=%20)
)
- [ ] No flaky tests detected (100 iterations on both Windows & Linux)
- [ ] README.md is up to date
- [ ] CHANGELOG.md is updated with v1.0.0 changes (create if doesn't exist)
- [ ] All planned features for v1.0.0 are complete
- [ ] You have a NuGet.org account (create at https://www.nuget.org/users/account/LogOn)

---

## Part 1: Prepare for Release

### Step 1: Create a Changelog (if you don't have one)

Create `CHANGELOG.md` in your repository root:

```markdown
# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.0] - 2025-01-XX

### Added
- `SelectParallelAsync` - Transform collections in parallel with bounded concurrency
- `SelectParallelStreamAsync` - Stream results as they complete
- `ForEachParallelAsync` - Execute side effects in parallel
- Flexible error handling modes: FailFast, CollectAndContinue, BestEffort
- Retry policy with exponential backoff and transient error detection
- Per-item timeout support
- Lifecycle hooks: OnStart, OnComplete, OnError, OnThrottle
- Cancellation token support throughout
- Support for .NET 8.0 and .NET 9.0

### Fixed
- OnErrorAsync callback now properly invoked in FailFast mode
- SelectParallelStreamAsync cancellation race condition resolved

### Documentation
- Comprehensive README with examples
- CI/CD pipeline with 200-iteration flaky test detection
- 99.5% code coverage
```

**Commit the changelog:**
```bash
git add CHANGELOG.md
git commit -m "Add CHANGELOG for v1.0.0"
git push origin master
```

---

### Step 2: Verify Package Metadata

Check `src/Rivulet.Core/Rivulet.Core.csproj` contains correct information:

```xml
<PropertyGroup>
    <TargetFrameworks>net9.0;net8.0;</TargetFrameworks>
    <Nullable>enable</Nullable>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
    <PackageId>Rivulet.Core</PackageId>
    <Title>Rivulet.Core</Title>
    <Authors>Jeffeek</Authors>
    <Description>Safe, async-first parallel operators with bounded concurrency, retries, and backpressure for I/O-heavy workloads.</Description>
    <PackageTags>async;parallel;linq;throttling;io;dotnet;channels</PackageTags>
    <RepositoryUrl>https://github.com/Jeffeek/Rivulet</RepositoryUrl>
    <PackageLicenseExpression>MIT</PackageLicenseExpression>
    <PackageProjectUrl>https://github.com/Jeffeek/Rivulet</PackageProjectUrl>
    <PackageReadmeFile>README.md</PackageReadmeFile>
    <IncludeSymbols>true</IncludeSymbols>
    <SymbolPackageFormat>snupkg</SymbolPackageFormat>
</PropertyGroup>
```

**If you made changes, commit them:**
```bash
git add src/Rivulet.Core/Rivulet.Core.csproj
git commit -m "Update package metadata for v1.0.0"
git push origin master
```

---

### Step 3: Create Release Branch

```bash
# Create release branch
git checkout -b release/1.0.0

# Push to GitHub
git push origin release/1.0.0
```

---

### Step 4: Final Verification

Run tests locally one more time:

```bash
# Clean build
dotnet clean
dotnet restore
dotnet build -c Release

# Run tests
dotnet test -c Release

# Test package locally
dotnet pack -c Release -p:PackageVersion=1.0.0 --output ./test-packages

# Inspect package
# Windows PowerShell:
Expand-Archive ./test-packages/Rivulet.Core.1.0.0.nupkg -DestinationPath ./test-extract
dir ./test-extract

# Linux/Mac:
unzip -l ./test-packages/Rivulet.Core.1.0.0.nupkg
```

Verify the package contains:
- ✅ `lib/net8.0/Rivulet.Core.dll`
- ✅ `lib/net9.0/Rivulet.Core.dll`
- ✅ `README.md`
- ✅ XML documentation files
- ✅ Dependencies listed correctly

---

## Part 2: Create Git Tag and Trigger Release

### Step 5: Create and Push Git Tag

This is the critical step that triggers the release workflow:

```bash
# Make sure you're on the release branch
git checkout release/1.0.0

# Create annotated tag
git tag -a v1.0.0 -m "Release version 1.0.0

First stable release of Rivulet.Core

Features:
- Parallel async operators with bounded concurrency
- Flexible error handling modes
- Retry policies with exponential backoff
- Per-item timeouts and lifecycle hooks
- Support for .NET 8.0 and 9.0
- 99.5% code coverage
"

# Push the tag (THIS TRIGGERS THE RELEASE WORKFLOW)
git push origin v1.0.0
```

---

### Step 6: Monitor the Release Workflow

1. Go to your GitHub repository
2. Click on "Actions" tab
3. You should see a new workflow run: "Release Package"
4. Click on it to watch progress

The workflow will:
1. ✅ Checkout code
2. ✅ Extract version from tag (`1.0.0`)
3. ✅ Restore dependencies
4. ✅ Build in Release mode
5. ✅ Run all tests (single iteration sanity check)
6. ✅ Pack NuGet package with version `1.0.0`
7. ✅ Create GitHub Release
8. ✅ Attach `.nupkg` file to release
9. ✅ Upload artifacts

**Expected duration:** ~3-5 minutes

---

### Step 7: Verify GitHub Release

Once the workflow completes:

1. Go to your repository
2. Click "Releases" (right sidebar)
3. You should see "v1.0.0" release with:
   - ✅ Automatically generated release notes
   - ✅ `Rivulet.Core.1.0.0.nupkg` attached
   - ✅ `Rivulet.Core.1.0.0.snupkg` attached (symbols)

4. **Download the package** and verify it locally:

```bash
# Download from GitHub release
# Extract and inspect
unzip -l Rivulet.Core.1.0.0.nupkg

# Or test install in a sample project
mkdir test-project
cd test-project
dotnet new console
dotnet add package Rivulet.Core --source path/to/downloads --version 1.0.0
```

---

## Part 3: Publish to NuGet.org

### Step 8: Get Your NuGet API Key

1. Go to https://www.nuget.org/
2. Sign in (or create account if you don't have one)
3. Click your username → "API Keys"
4. Click "Create"
   - **Key Name**: `Rivulet CI/CD`
   - **Expiration**: 365 days (or longer)
   - **Scopes**: `Push new packages and package versions`
   - **Glob Pattern**: `Rivulet.Core`
5. Click "Create"
6. **Copy the API key immediately** (you won't see it again!)

---

### Step 9: Add API Key to GitHub Secrets

1. Go to your GitHub repository
2. Click "Settings" → "Secrets and variables" → "Actions"
3. Click "New repository secret"
   - **Name**: `NUGET_API_KEY`
   - **Secret**: Paste your NuGet API key
4. Click "Add secret"

---

### Step 10: Option A - Re-run Existing Workflow (Recommended)

Since you already have the v1.0.0 tag and GitHub release:

1. Go to Actions → "Release Package" → Latest run
2. Click "Re-run jobs" → "Re-run all jobs"
3. This will publish the existing package to NuGet.org

**OR**

### Step 11: Option B - Create a New Tag

If you want to start fresh:

```bash
# Delete the tag locally and remotely
git tag -d v1.0.0
git push origin :refs/tags/v1.0.0

# Delete the GitHub release manually (via GitHub UI)

# Create tag again
git tag -a v1.0.0 -m "Release version 1.0.0"
git push origin v1.0.0
```

---

### Step 12: Verify NuGet Publication

1. **Monitor the workflow** - it should complete successfully with the publish step
2. **Check NuGet.org**:
   - Go to https://www.nuget.org/packages/Rivulet.Core/
   - You should see version `1.0.0` listed (may take 5-10 minutes to appear)
3. **Verify package can be installed**:

```bash
mkdir nuget-test
cd nuget-test
dotnet new console
dotnet add package Rivulet.Core --version 1.0.0
```

---

## Part 4: Post-Release

### Step 13: Merge Release Branch

```bash
# Switch to master
git checkout master

# Merge release branch
git merge release/1.0.0

# Push to GitHub
git push origin master

# Optional: Delete release branch
git branch -d release/1.0.0
git push origin --delete release/1.0.0
```

---

### Step 14: Announce Release

1. **Twitter/X**: "Just released Rivulet v1.0.0! 🚀 Safe, async-first parallel operators for .NET https://nuget.org/packages/Rivulet.Core"

2. **Reddit** (r/dotnet, r/csharp):
   ```
   [Release] Rivulet v1.0.0 - Async-first parallel operators with bounded concurrency

   I'm excited to announce the first stable release of Rivulet.Core!

   Features:
   - SelectParallelAsync, SelectParallelStreamAsync, ForEachParallelAsync
   - Bounded concurrency with backpressure
   - Flexible error handling (FailFast, CollectAndContinue, BestEffort)
   - Retry policies with exponential backoff
   - Per-item timeouts and lifecycle hooks
   - 99.5% code coverage

   NuGet: https://nuget.org/packages/Rivulet.Core
   GitHub: https://github.com/Jeffeek/Rivulet

   Feedback welcome!
   ```

3. **GitHub Discussions**: Create a discussion thread announcing the release

---

## Troubleshooting

### Issue: Tag already exists

```bash
# Delete local tag
git tag -d v1.0.0

# Delete remote tag
git push origin :refs/tags/v1.0.0

# Delete GitHub release manually

# Create new tag
git tag -a v1.0.0 -m "Release 1.0.0"
git push origin v1.0.0
```

### Issue: NuGet publish fails with 409 Conflict

This means the package version already exists on NuGet.org. You cannot republish the same version.

**Solution**: Increment the version:
```bash
# Delete the v1.0.0 tag
git tag -d v1.0.0
git push origin :refs/tags/v1.0.0

# Create v1.0.1
git tag -a v1.0.1 -m "Release 1.0.1"
git push origin v1.0.1
```

### Issue: Workflow fails with "Package already exists"

This is expected if you re-run a workflow. The `--skip-duplicate` flag prevents the error, but it won't re-upload.

**Solution**: This is fine - the package is already on NuGet.org.

---

## Future Releases

For subsequent releases (v1.1.0, v1.2.0, v2.0.0):

1. Update CHANGELOG.md
2. Create release branch: `release/x.y.z`
3. Create tag: `git tag -a vx.y.z -m "Release x.y.z"`
4. Push tag: `git push origin vx.y.z`
5. Workflow automatically publishes to NuGet
6. Merge release branch to master

---

## Quick Command Reference

```bash
# Create release branch
git checkout -b release/1.0.0
git push origin release/1.0.0

# Create and push tag
git tag -a v1.0.0 -m "Release 1.0.0"
git push origin v1.0.0

# Merge to master
git checkout master
git merge release/1.0.0
git push origin master

# Delete old tag (if needed)
git tag -d v1.0.0
git push origin :refs/tags/v1.0.0
```

---

## Checklist: Ready to Release?

- [ ] All tests pass on master
- [ ] Flaky test detection passes (200 iterations, Windows + Linux)
- [ ] Code coverage ≥ 99%
- [ ] CHANGELOG.md updated
- [ ] Package metadata correct in .csproj
- [ ] README.md up to date
- [ ] Local package test successful
- [ ] Release branch created
- [ ] Git tag created and pushed
- [ ] GitHub release verified
- [ ] NuGet API key added to secrets
- [ ] NuGet publish step enabled in workflow
- [ ] Package published to NuGet.org
- [ ] Package installation verified
- [ ] Release announced
- [ ] Release branch merged to master

---

## Questions?

If you encounter issues:
1. Check GitHub Actions logs for detailed error messages
2. Review the workflow file: `.github/workflows/release.yml`
3. Check NuGet.org for existing package versions
4. Verify your API key has correct permissions

**Congratulations on your first release!** 🎉
