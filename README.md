<div align="center">
  <img src="assets/logo.png" alt="Rivulet.Core Logo" width="600">
</div>

<div align="center">

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
---
[![NuGet](https://img.shields.io/nuget/v/Rivulet.Core)](https://www.nuget.org/packages/Rivulet.Core/)
![NuGet Downloads](https://img.shields.io/nuget/dt/Rivulet.Core)
---
![CI/CD Pipeline (build+test)](https://img.shields.io/github/actions/workflow/status/Jeffeek/Rivulet/github-workflow.yml?label=build)
![CI/CD Pipeline (release)](https://img.shields.io/github/actions/workflow/status/Jeffeek/Rivulet/release.yml?label=release)
---
[![Codecov](https://codecov.io/gh/Jeffeek/Rivulet/branch/master/graph/badge.svg)](https://codecov.io/gh/Jeffeek/Rivulet)
[![.NET](https://img.shields.io/badge/.NET-8.0%20%7C%209.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)

</div>

---

**Safe, async-first parallel operators with bounded concurrency, retries, cancellation, and streaming backpressure for I/O-heavy workloads.**

- Async-first (`ValueTask`), works with `IEnumerable<T>` and `IAsyncEnumerable<T>`
- Bounded concurrency with backpressure (Channels)
- Retry policy with transient detection and exponential backoff
- Per-item timeouts, cancellation, lifecycle hooks
- Flexible error modes: FailFast, CollectAndContinue, BestEffort

### Install
```dotnet add package Rivulet.Core```

### Quick start
```csharp
var results = await urls.SelectParallelAsync(
    async (url, ct) =>
    {
        using var resp = await http.GetAsync(url, ct);
        resp.EnsureSuccessStatusCode();
        return (url, (int)resp.StatusCode);
    },
    new ParallelOptionsRivulet {
        MaxDegreeOfParallelism = 32,
        MaxRetries = 3,
        IsTransient = ex => ex is HttpRequestException or TaskCanceledException,
        ErrorMode = ErrorMode.CollectAndContinue
    });
```

### Streaming

```csharp
await foreach (var r in source.SelectParallelStreamAsync(
    async (x, ct) => await ComputeAsync(x, ct),
    new ParallelOptionsRivulet { MaxDegreeOfParallelism = 16 }))
{
    // consume incrementally
}
```

## Development Scripts

The repository includes PowerShell scripts to streamline development and release workflows:

### Build.ps1
Build, restore, and test the solution locally.

```powershell
# Debug build with tests (default)
.\Build.ps1

# Release build with tests
.\Build.ps1 -Configuration Release

# Skip tests
.\Build.ps1 -SkipTests
```

### NugetPackage.ps1
Build and inspect NuGet packages locally before releasing.

```powershell
# Build with test version
.\NugetPackage.ps1

# Build with specific version
.\NugetPackage.ps1 -Version "1.2.3"
```

Creates package in `./test-packages` and extracts contents to `./test-extract` for verification.

### SmartCommit.ps1
Generate high-quality commit messages using AI (Claude, Gemini, or OpenAI).

```powershell
# Quick setup - set API key for your preferred provider
$env:ANTHROPIC_API_KEY = "your-key"  # For Claude
$env:GOOGLE_API_KEY = "your-key"      # For Gemini
$env:OPENAI_API_KEY = "your-key"      # For OpenAI

# Auto-detect provider from environment
.\SmartCommit.ps1

# Or specify provider explicitly
.\SmartCommit.ps1 -Provider Claude
.\SmartCommit.ps1 -Provider Gemini
.\SmartCommit.ps1 -Provider OpenAI
```

**Advanced**: Create `.smartcommit.config.json` (see `.smartcommit.config.example.json`) to configure:
- Default provider
- API keys (alternative to environment variables)
- Model versions (claude-3-5-sonnet, gemini-2.0-flash, gpt-4o, etc.)

This script:
- Analyzes your staged changes using git diff
- Calls your chosen AI provider to generate a meaningful commit message
- Shows the suggested message and allows you to:
  - **[y]** Accept and commit
  - **[r]** Request revision with feedback (e.g., "make it shorter", "add more detail")
  - **[n]** Cancel
- Iteratively refines the message based on your feedback
- Commits changes when you accept

**Get API keys**:
- Claude: [console.anthropic.com](https://console.anthropic.com/)
- Gemini: [aistudio.google.com/apikey](https://aistudio.google.com/apikey)
- OpenAI: [platform.openai.com/api-keys](https://platform.openai.com/api-keys)

### Release.ps1
Create release branch, tag, and trigger automated publishing.

```powershell
# Create release for version 1.0.0 (creates branch release/1.0.x, tag v1.0.0)
.\Release.ps1 -Version "1.0.0"

# Create patch release 1.0.1 (uses existing branch release/1.0.x, tag v1.0.1)
.\Release.ps1 -Version "1.0.1"

# Create pre-release (creates branch release/2.0.x, tag v2.0.0-beta)
.\Release.ps1 -Version "2.0.0-beta"
```

**Branching Strategy**:
- Branches: `release/{major}.{minor}.x` (e.g., `release/1.0.x` for all 1.0.* versions)
- Tags: `v{full.version}` (e.g., `v1.0.0`, `v1.0.1`, `v1.0.2`)
- Master branch for active development
- Patch releases reuse the same release branch

This script:
- Creates/switches to `release/{major}.{minor}.x` branch
- Displays release information (commit details, author, version, repository)
- **Asks for confirmation (y/Y) before proceeding**
- Creates git tag `v{version}` and pushes to GitHub
- Triggers the release workflow that builds, tests, and publishes to NuGet.org

The confirmation step shows:
- Version and tag information
- Current commit hash, author, date, and message
- List of actions that will be performed
- Allows you to cancel before any changes are pushed

### Roadmap

- Metrics via EventCounters
- Built-in retry policies (Jitter)
- Ordered output mode (optional)
- Batching operator
