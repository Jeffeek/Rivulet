# Release Guide: Publishing Rivulet to NuGet

This guide walks you through creating and publishing Rivulet releases to NuGet.org.

## Rivulet Package Ecosystem

Rivulet v1.2.0 consists of **5 NuGet packages**:

1. **Rivulet.Core** - Core parallel operators with bounded concurrency
2. **Rivulet.Diagnostics** - EventListeners, metrics aggregation, Prometheus export, health checks
3. **Rivulet.Diagnostics.OpenTelemetry** - OpenTelemetry integration for distributed tracing
4. **Rivulet.Testing** - Testing utilities (VirtualTimeProvider, FakeChannel, ChaosInjector, ConcurrencyAsserter)
5. **Rivulet.Hosting** - Microsoft.Extensions.Hosting integration, background services, configuration binding

All 5 packages are built and published together with the same version number.

---

## Prerequisites Checklist

Before starting the release process, ensure:

- [ ] All CI tests pass on `master` branch
- [ ] Code coverage is at expected (90%>=) level (currently ![Codecov (with branch)](https://img.shields.io/codecov/c/github/Jeffeek/Rivulet/master?style=flat&label=%20)
)
- [ ] No flaky tests detected (100 iterations on both Windows & Linux)
- [ ] README.md (GitHub repository) is up to date
- [ ] All 5 package README files are up to date (src/*/README.md)
- [ ] CHANGELOG.md is updated with version changes (create if doesn't exist)
- [ ] All planned features for the version are complete
- [ ] You have a NuGet.org account (create at https://www.nuget.org/users/account/LogOn)
- [ ] NuGet API key has glob pattern `Rivulet.*` to cover all packages

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
- 90%+ code coverage
```

**Commit the changelog:**
```bash
git add CHANGELOG.md
git commit -m "Add CHANGELOG for v1.0.0"
git push origin master
```

---

### Step 2: Verify Package Metadata

Check all 5 .csproj files contain correct information. Example from `src/Rivulet.Core/Rivulet.Core.csproj`:

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
    <PackageIcon>nuget_logo.png</PackageIcon>
    <PackageReadmeFile>README.md</PackageReadmeFile>
    <IncludeSymbols>true</IncludeSymbols>
    <SymbolPackageFormat>snupkg</SymbolPackageFormat>
</PropertyGroup>

<ItemGroup>
    <None Include="..\..\assets\nuget_logo.png" Pack="true" PackagePath="\" />
    <!-- Pack README.md from repo as README.md in package -->
    <None Include="README.md" Pack="true" PackagePath="\README.md" />
</ItemGroup>
```

**If you made changes, commit them:**
```bash
git add src/Rivulet.Core/Rivulet.Core.csproj PACKAGE_README.md
git commit -m "Update package metadata and README for v1.0.0"
git push origin master
```

---

### Step 3: Create Release

**Recommended: Use the automated Release.ps1 script:**

```powershell
# This creates release/1.0.x branch and v1.0.0 tag
.\Release.ps1 -Version "1.0.0"
```

The script will:
- Extract major.minor from version (1.0)
- Create/switch to `release/1.0.x` branch
- Show commit details and ask for confirmation
- Create tag `v1.0.0` and push everything
- Trigger the GitHub Actions release workflow

**Manual Alternative (if not using Release.ps1):**

```bash
# Create release branch (note: uses .x pattern for all 1.0.* patches)
git checkout -b release/1.0.x

# Push to GitHub
git push origin release/1.0.x

# Create and push tag
git tag -a v1.0.0 -m "Release 1.0.0"
git push origin v1.0.0
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

# Inspect package contents
# Windows PowerShell (copy to .zip first, as .nupkg files are zip archives):
Copy-Item ./test-packages/Rivulet.Core.1.0.0.nupkg ./test-packages/Rivulet.Core.1.0.0.zip
Expand-Archive ./test-packages/Rivulet.Core.1.0.0.zip -DestinationPath ./test-extract
dir ./test-extract -Recurse

# OR use 7-Zip (if installed):
# 7z l ./test-packages/Rivulet.Core.1.0.0.nupkg

# Linux/Mac (rename to .zip first):
cp ./test-packages/Rivulet.Core.1.0.0.nupkg ./test-packages/Rivulet.Core.1.0.0.zip
unzip -l ./test-packages/Rivulet.Core.1.0.0.zip

# OR directly with unzip (works on most Linux systems):
# unzip -l ./test-packages/Rivulet.Core.1.0.0.nupkg
```

Verify the package contains:
- ✅ `lib/net8.0/Rivulet.Core.dll`
- ✅ `lib/net9.0/Rivulet.Core.dll`
- ✅ `README.md`
- ✅ `nuget_logo.png` (in package root - package icon)
- ✅ XML documentation files (`.xml`)
- ✅ Dependencies listed correctly in `.nuspec`

---

## Part 2: Create Git Tag and Trigger Release

### Step 5: Create and Push Git Tag

**If you used Release.ps1 in Step 3, skip this step - it's already done!**

**Manual Alternative:** This is the critical step that triggers the release workflow:

```bash
# Make sure you're on the release branch (note: .x pattern)
git checkout release/1.0.x

# Create annotated tag
git tag -a v1.0.0 -m "Release version 1.0.0

First stable release of Rivulet.Core

Features:
- Parallel async operators with bounded concurrency
- Flexible error handling modes
- Retry policies with exponential backoff
- Per-item timeouts and lifecycle hooks
- Support for .NET 8.0 and 9.0
- 90%+ code coverage
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
# Download from GitHub release, then inspect

# Windows PowerShell:
Copy-Item Rivulet.Core.1.0.0.nupkg Rivulet.Core.1.0.0.zip
Expand-Archive Rivulet.Core.1.0.0.zip -DestinationPath ./extracted
dir ./extracted -Recurse

# Linux/Mac:
cp Rivulet.Core.1.0.0.nupkg Rivulet.Core.1.0.0.zip
unzip -l Rivulet.Core.1.0.0.zip

# Test install in a sample project (all platforms):
mkdir test-project
cd test-project
dotnet new console
dotnet add package Rivulet.Core --source ../path/to/downloads --version 1.0.0
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

### Step 13: Merge Release Branch (Optional)

With the new branching strategy (`release/{major}.{minor}.x`), release branches are kept alive for future patches.

```bash
# Switch to master
git checkout master

# Merge release branch (if changes need to be back-ported)
git merge release/1.0.x

# Push to GitHub
git push origin master

# NOTE: Do NOT delete the release branch!
# The release/1.0.x branch stays alive for future patches (1.0.1, 1.0.2, etc.)
# Only delete release branches when the major.minor version is completely EOL
```

**For future patch releases (e.g., 1.0.1):**
- Use the existing `release/1.0.x` branch
- Create new tag `v1.0.1` on that branch
- The branch persists for all 1.0.* versions

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
   - 90%+ code coverage

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

### Issue: NuGet publish fails with 403 Forbidden

**Error message**:
```
error: Response status code does not indicate success: 403 (The specified API key is invalid,
has expired, or does not have permission to access the specified package.).
```

**Causes**:
1. **API key expired** - NuGet API keys expire after the set duration
2. **API key scope** - The key doesn't have permission for the package being pushed
3. **Wrong package being pushed** - Trying to push test/sample projects instead of library

**Solutions**:

**Option 1: Check API Key Scope**
1. Go to https://www.nuget.org/account/apikeys
2. Find your API key
3. Check the "Glob Pattern" - it should be `Rivulet.Core` or `Rivulet.*`
4. If it's wrong, create a new API key with correct scope
5. Update the `NUGET_API_KEY` secret in GitHub

**Option 2: Verify Only Library Package is Being Pushed**

The workflow should ONLY push `Rivulet.Core.*.nupkg`, not test or sample projects.

Check the workflow file (`.github/workflows/release.yml`):
```yaml
# Line 46 - Should pack ONLY the library project
- name: Pack NuGet package
  run: dotnet pack src/Rivulet.Core/Rivulet.Core.csproj -c Release --no-build --output ./artifacts

# Line 69 - Should push ONLY Rivulet.Core packages
- name: Publish to NuGet.org
  run: dotnet nuget push ./artifacts/Rivulet.Core.*.nupkg --api-key ${{ secrets.NUGET_API_KEY }} ...
```

**Option 3: Regenerate API Key**
1. Go to https://www.nuget.org/account/apikeys
2. Click "Regenerate" on your existing key (or create new one)
3. Set **Glob Pattern** to `Rivulet.*` (to cover all Rivulet packages)
4. Copy the new key
5. Update GitHub secret: Settings → Secrets and variables → Actions → `NUGET_API_KEY`

---

### Issue: Multiple .nupkg files in GitHub Release (ConsoleSample + Library packages)

**Problem**: Both sample/test projects and library packages appear in the release assets.

**Cause**: The `dotnet pack` command was packing ALL projects in the solution.

**Solution** (Already fixed in latest workflow):

1. **Workflow now packs only library projects** (`.github/workflows/release.yml` lines 45-51):
   ```yaml
   run: |
     dotnet pack src/Rivulet.Core/Rivulet.Core.csproj -c Release --output ./artifacts -p:PackageVersion=$VERSION
     dotnet pack src/Rivulet.Diagnostics/Rivulet.Diagnostics.csproj -c Release --output ./artifacts -p:PackageVersion=$VERSION
     dotnet pack src/Rivulet.Diagnostics.OpenTelemetry/Rivulet.Diagnostics.OpenTelemetry.csproj -c Release --output ./artifacts -p:PackageVersion=$VERSION
     dotnet pack src/Rivulet.Testing/Rivulet.Testing.csproj -c Release --output ./artifacts -p:PackageVersion=$VERSION
     dotnet pack src/Rivulet.Hosting/Rivulet.Hosting.csproj -c Release --output ./artifacts -p:PackageVersion=$VERSION
   ```

2. **ConsoleSample project marked as non-packable** (`samples/Rivulet.ConsoleSample/Rivulet.ConsoleSample.csproj`):
   ```xml
   <PropertyGroup>
     <IsPackable>false</IsPackable>
   </PropertyGroup>
   ```

3. **Test projects already marked as non-packable**:
   ```xml
   <PropertyGroup>
     <IsPackable>false</IsPackable>
   </PropertyGroup>
   ```

**To Clean Up Existing Release**:
1. Go to your GitHub release
2. Click "Edit"
3. Delete unwanted `.nupkg` files (sample/test projects)
4. Save the release

**For Next Release**: The workflow will now automatically only include the 5 library packages.

---

## Future Releases

### For Minor/Major Releases (v1.1.0, v2.0.0):

**Recommended - Use Release.ps1:**
```powershell
.\Release.ps1 -Version "1.1.0"  # Creates release/1.1.x branch and v1.1.0 tag
```

**Manual Alternative:**
1. Update CHANGELOG.md
2. Create release branch: `release/{major}.{minor}.x` (e.g., `release/1.1.x`)
3. Create tag: `git tag -a v{version} -m "Release {version}"`
4. Push: `git push origin release/{major}.{minor}.x && git push origin v{version}`
5. Workflow automatically publishes to NuGet

### For Patch Releases (v1.0.1, v1.0.2):

**Recommended - Use Release.ps1:**
```powershell
.\Release.ps1 -Version "1.0.1"  # Reuses existing release/1.0.x branch, creates v1.0.1 tag
```

**Manual Alternative:**
1. Update CHANGELOG.md
2. Switch to existing branch: `git checkout release/1.0.x`
3. Create tag: `git tag -a v1.0.1 -m "Release 1.0.1"`
4. Push tag: `git push origin v1.0.1`
5. Workflow automatically publishes to NuGet

---

## Quick Command Reference

**Automated (Recommended):**
```powershell
# New minor/major release (creates new release/x.y.x branch)
.\Release.ps1 -Version "1.1.0"

# Patch release (reuses existing release/1.0.x branch)
.\Release.ps1 -Version "1.0.1"

# Pre-release
.\Release.ps1 -Version "2.0.0-beta"
```

**Manual Alternative:**
```bash
# Create new release branch (for new minor/major version)
git checkout -b release/1.1.x
git push origin release/1.1.x

# OR switch to existing release branch (for patches)
git checkout release/1.0.x

# Create and push tag
git tag -a v1.0.1 -m "Release 1.0.1"
git push origin v1.0.1

# Merge to master (optional)
git checkout master
git merge release/1.0.x
git push origin master

# Delete old tag (if needed)
git tag -d v1.0.0
git push origin :refs/tags/v1.0.0
```

---

## Checklist: Ready to Release?

- [ ] All tests pass on master
- [ ] Flaky test detection passes (100 iterations, Windows + Linux)
- [ ] Code coverage ≥ 90%
- [ ] CHANGELOG.md updated
- [ ] Package metadata correct in .csproj
- [ ] README.md (GitHub repository) up to date
- [ ] PACKAGE_README.md (repo, packed as README.md in package) up to date
- [ ] Local package test successful (verify README.md in extracted package)
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
