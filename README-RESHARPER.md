# ReSharper Code Analysis Workflow

This document explains how the ReSharper code analysis workflow works and how to use it.

## Overview

The `resharper-analysis.yml` workflow runs JetBrains ReSharper InspectCode on pull requests to catch code quality issues early.

## When It Runs

The workflow triggers automatically on pull requests when:
- C# files (`**.cs`) are modified
- Project files (`**.csproj`) are modified
- Editor config (`.editorconfig`) is modified
- The workflow file itself is modified

Target branches: `master` and `release/**`

## What It Does

1. **Installs ReSharper Command Line Tools** (2025.3.0.4)
   - Installed via dotnet tool install
   - Uses the official JetBrains.ReSharper.GlobalTools package

2. **Builds the solution**
   - Restores NuGet packages
   - Builds in Release configuration

3. **Runs InspectCode Analysis**
   - Analyzes all C# code in the solution
   - Reports issues with severity WARNING and above
   - Excludes build artifacts, test projects, and sample projects

4. **Generates Report**
   - Creates XML report with detailed findings
   - Extracts summary with top 10 issues
   - Uploads full report as workflow artifact

5. **Comments on PR**
   - Posts analysis summary directly on the PR
   - Updates existing comment if workflow runs again
   - Shows issue count, types, and locations

6. **Provides Guidance**
   - Highlights issues that need attention
   - Links to specific files and line numbers
   - Suggests improvements

## Report Severity Levels

The workflow reports issues at these levels:
- **ERROR**: Critical issues that should be fixed immediately
- **WARNING**: Important issues that should be addressed
- **SUGGESTION**: Code improvements (filtered out by default)
- **HINT**: Minor suggestions (filtered out by default)

Currently configured to show: **WARNING and above**

## Excluded Directories

The following directories are excluded from analysis:
- `**/obj/**` - Build output (temp files)
- `**/bin/**` - Compiled binaries
- `**/TestResults/**` - Test results and coverage
- `**/samples/**` - Samples
- `**/tests/**` - Tests

## Example PR Comment

When the workflow completes, it posts a comment like this:

```markdown
## ReSharper Code Analysis Summary

⚠️ Found **3** issue(s) that need attention.

### Top Issues
- **PossibleNullReferenceException** in `MyClass.cs:45`
  - Possible 'NullReferenceException' when accessing property
- **UnusedMember.Local** in `Helper.cs:23`
  - Local function 'ProcessData' is never used
- **RedundantUsingDirective** in `Program.cs:5`
  - Using directive is not required by the code

---
📊 **Analysis Tool**: JetBrains ReSharper 2025.3.0.4
🔍 **Severity Level**: WARNING and above
```

## Viewing the Full Report

1. Go to the workflow run in the Actions tab
2. Scroll to the bottom to find "Artifacts"
3. Download `resharper-report`
4. Open `resharper-report.xml` in a text editor or IDE
5. Review all issues with full details

## Handling Issues

### If Issues Are Found

1. **Review the summary** in the PR comment
2. **Download the full report** for detailed analysis
3. **Fix the issues** in your branch
4. **Push the changes** - the workflow will run again
5. **Verify** the updated comment shows fewer issues

### If No Issues Are Found

The workflow will post:
```markdown
✅ No issues found! Code quality is excellent.
```

## Integration with Existing Workflows

This workflow runs in parallel with:
- `ci.yml` - Main CI/CD pipeline (build, test, coverage)
- `codeql.yml` - Security analysis
- `flaky-test-detection.yml` - Reliability testing

All workflows must pass for the PR to be merged.

## Workflow Behavior

- **Does NOT fail the build** - Issues are reported as warnings
- **Comments are updated** - Re-running updates the existing comment
- **NuGet caching enabled** - NuGet packages are cached for faster runs
- **Timeout: 15 minutes** - Prevents hanging builds

## Manual Trigger

To run ReSharper analysis manually:

```bash
# Install ReSharper Command Line Tools
dotnet new tool-manifest --force
dotnet tool install JetBrains.ReSharper.GlobalTools --version 2025.3.0.4
dotnet tool restore

# Build the solution first
dotnet restore
dotnet build -c Release --no-restore

# Run analysis
dotnet jb inspectcode Rivulet.sln \
  --output=resharper-report.xml \
  --format=Xml \
  --no-build \
  --properties:Configuration=Release \
  --severity=WARNING \
  --exclude=**/obj/**;**/bin/**;**/TestResults/**;**/samples/**;**/tests/** \
  --verbosity=WARN
```

## Customization

### Adjusting Severity Level

Edit `.github/workflows/resharper-analysis.yml`:

```yaml
--severity=WARNING  # Change to ERROR, SUGGESTION, or HINT
```

### Excluding Additional Directories

Edit the `--exclude` parameter:

```yaml
--exclude=**/obj/**;**/bin/**;**/TestResults/**;**/MyCustomDir/**
```

### Changing ReSharper Version

Update the version in these places:
1. Tool install command: `--version 2025.3.0.4`
2. Summary message: `JetBrains ReSharper 2025.3.0.4`

## Troubleshooting

### Workflow Fails to Install ReSharper

- Check if the version exists: https://www.nuget.org/packages/JetBrains.ReSharper.GlobalTools
- Verify network connectivity in the runner
- Check dotnet tool install output for errors

### No Report Generated

- Check if the solution file path is correct
- Verify the build succeeded before analysis
- Review workflow logs for error messages

### Too Many False Positives

1. Add suppressions to `.editorconfig`
2. Use `#pragma warning disable` for specific cases
3. Adjust severity level to show only errors

### Report Not Posted to PR

- Verify the workflow has `pull-requests: write` permission
- Check if the PR is from a fork (permissions may be limited)
- Review the "Comment PR with Results" step logs

## Further Reading

- [ReSharper Command Line Tools Documentation](https://www.jetbrains.com/help/resharper/InspectCode.html)
- [ReSharper Code Inspections](https://www.jetbrains.com/help/resharper/Code_Analysis__Index.html)
- [GitHub Actions Documentation](https://docs.github.com/en/actions)

---

**Last Updated**: 2025-12-13
**ReSharper Version**: 2025.3.0.4
**Workflow File**: `.github/workflows/resharper-analysis.yml`
