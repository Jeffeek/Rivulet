# Contributing to Rivulet

Thank you for your interest in contributing to Rivulet! This guide will help you get started.

## Table of Contents
- [Code of Conduct](#code-of-conduct)
- [How Can I Contribute?](#how-can-i-contribute)
- [Development Setup](#development-setup)
- [Coding Standards](#coding-standards)
- [Testing Requirements](#testing-requirements)
- [Pull Request Process](#pull-request-process)
- [Project Structure](#project-structure)

---

## Code of Conduct

This project adheres to the [Contributor Covenant Code of Conduct](CODE_OF_CONDUCT.md). By participating, you are expected to uphold this code. Please report unacceptable behavior to the project maintainers.

---

## How Can I Contribute?

### Reporting Bugs

Before creating bug reports, please check existing issues to avoid duplicates. When creating a bug report, use the [bug report template](.github/ISSUE_TEMPLATE/bug_report.md) and include:

- A clear, descriptive title
- Exact steps to reproduce the problem
- Expected behavior vs actual behavior
- Code samples demonstrating the issue
- Your environment (.NET version, OS, Rivulet version)

### Suggesting Features

Feature requests are welcome! Use the [feature request template](.github/ISSUE_TEMPLATE/feature_request.md) and include:

- Clear use case and motivation
- Proposed API design (if applicable)
- Alternative approaches you've considered
- Impact on existing functionality

### Contributing Code

We welcome pull requests for:

- Bug fixes
- Performance improvements
- Documentation improvements
- New features (discuss in an issue first for large changes)
- Test coverage improvements

---

## Development Setup

### Prerequisites

- **.NET SDK**: 8.0 and 9.0 (for multi-targeting)
- **IDE**: Visual Studio 2022, JetBrains Rider, or VS Code with C# extension
- **Git**: For version control
- **Docker Desktop** (optional, required for integration tests):
  - **Windows**: [Docker Desktop for Windows](https://www.docker.com/products/docker-desktop/)
  - **Linux**: Docker Engine or Docker Desktop
  - **macOS**: [Docker Desktop for Mac](https://www.docker.com/products/docker-desktop/)

> **Note**: Docker is only required for running integration tests that use [Testcontainers](https://dotnet.testcontainers.org/) (e.g., SQL Server, MySQL, PostgreSQL bulk copy tests). Unit tests run without Docker.

### Clone and Build

```bash
# Clone the repository
git clone https://github.com/Jeffeek/Rivulet.git
cd Rivulet

# Restore dependencies
dotnet restore

# Build the solution
dotnet build -c Release

# Run tests
dotnet test -c Release
```

### Using Build Scripts

```powershell
# Build and test
.\Build.ps1

# Build without tests
.\Build.ps1 -SkipTests

# Create local NuGet package for testing
.\NugetPackage.ps1 -Version "1.0.0-local"
```

---

## Coding Standards

### General Principles

1. **Async-First**: Always use `ValueTask<T>` for performance, never `Task<T>` in hot paths
2. **Nullability**: `#nullable enable` everywhere, handle all nullable reference types
3. **Immutability**: Options classes use `init` properties, immutable after construction
4. **Performance**: Minimize allocations, use `ConfigureAwait(false)` consistently
5. **Safety**: Always respect cancellation tokens, enforce timeouts, handle errors explicitly

### Naming Conventions

- **Extension Methods**: `{Operation}Parallel{Mode}` pattern (e.g., `SelectParallelAsync`)
- **Options Classes**: Suffix with descriptive names (e.g., `ParallelOptionsRivulet`)
- **Enums**: Clear, descriptive names (e.g., `ErrorMode.FailFast`)
- **Internal Helpers**: `internal static class` pattern

### Code Style

```csharp
// ✅ Good: ValueTask, ConfigureAwait, explicit nullability
public static async ValueTask<List<TResult>> SelectParallelAsync<TSource, TResult>(
    this IEnumerable<TSource> source,
    Func<TSource, CancellationToken, ValueTask<TResult>> taskSelector,
    ParallelOptionsRivulet? options = null,
    CancellationToken cancellationToken = default)
{
    ArgumentNullException.ThrowIfNull(source);
    ArgumentNullException.ThrowIfNull(taskSelector);

    options ??= new ParallelOptionsRivulet();

    await DoWorkAsync().ConfigureAwait(false);
    // ...
}

// ❌ Bad: Task instead of ValueTask, no ConfigureAwait
public static async Task<List<TResult>> SelectParallelAsync<TSource, TResult>(
    this IEnumerable<TSource> source,
    Func<TSource, Task<TResult>> taskSelector)
{
    await DoWorkAsync(); // Missing ConfigureAwait(false)
    // ...
}
```

### XML Documentation

**Required** for all public APIs:

```csharp
/// <summary>
/// Transforms each element in parallel with bounded concurrency.
/// </summary>
/// <typeparam name="TSource">The type of elements in the source.</typeparam>
/// <typeparam name="TResult">The type of transformed elements.</typeparam>
/// <param name="source">The source collection.</param>
/// <param name="taskSelector">The transformation function.</param>
/// <param name="options">Configuration options.</param>
/// <param name="cancellationToken">Cancellation token.</param>
/// <returns>A task containing the list of results.</returns>
/// <exception cref="OperationCanceledException">When cancelled.</exception>
/// <exception cref="AggregateException">When CollectAndContinue mode has errors.</exception>
/// <remarks>
/// This method processes items in parallel with bounded concurrency to prevent resource exhaustion.
/// Use <see cref="SelectParallelStreamAsync{TSource, TResult}"/> for streaming scenarios.
/// </remarks>
```

---

## Testing Requirements

### Coverage Goals

- **Line Coverage**: ≥ 90%
- **Branch Coverage**: ≥ 90%
- **Flaky Tests**: 100% pass rate over 100 iterations on Windows + Linux (verified via weekly scheduled detection)

### Test Structure

```csharp
[Fact]
public async Task SelectParallelAsync_WithCancellation_ThrowsOperationCanceledException()
{
    // Arrange
    var source = Enumerable.Range(1, 100);
    var cts = new CancellationTokenSource();
    var options = new ParallelOptionsRivulet { MaxDegreeOfParallelism = 4 };

    // Act & Assert
    await Assert.ThrowsAsync<OperationCanceledException>(async () =>
    {
        await source.SelectParallelAsync(async (x, ct) =>
        {
            if (x == 10) cts.Cancel();
            await Task.Delay(10, ct);
            return x * 2;
        }, options, cts.Token);
    });
}
```

### Test Categories

1. **Unit Tests**: Test each method in isolation
2. **Integration Tests**: Test with real infrastructure (databases via Testcontainers)
   - Marked with `[Trait("Category", "Integration")]`
   - Require Docker Desktop to be running
   - Excluded from flaky test detection (deterministic but slow)
   - Use `IAsyncLifetime` per test class or `ICollectionFixture` for shared containers
3. **Error Handling Tests**: Verify all error modes (FailFast, CollectAndContinue, BestEffort)
4. **Edge Cases**: Empty collections, null handlers, cancellation, timeouts
5. **Concurrency Tests**: Verify parallelism limits, backpressure, race conditions
6. **Performance Tests**: Ensure no regressions (use BenchmarkDotNet)

### Running Tests

```bash
# Run all tests (requires Docker Desktop for integration tests)
dotnet test -c Release

# Run only unit tests (no Docker required)
dotnet test -c Release --filter "Category!=Integration"

# Run only integration tests (requires Docker Desktop)
dotnet test -c Release --filter "Category=Integration"

# Run with coverage
dotnet test -c Release --collect:"XPlat Code Coverage"

# Run specific test
dotnet test --filter "FullyQualifiedName~SelectParallelAsync_WithCancellation"

# Flaky test detection (manual trigger - excludes integration tests)
gh workflow run flaky-test-detection.yml -f iterations=20 -f timeout-minutes=40

# Note: Flaky detection runs automatically:
# - On every PR: 20 iterations (via ci.yml) - unit tests only
# - Weekly scheduled: 100 iterations (Sundays 3 AM UTC) - unit tests only
# - Integration tests excluded (deterministic but slow)
```

### Integration Test Guidelines

When adding integration tests that use Testcontainers:

1. **Mark with trait**: `[Trait("Category", "Integration")]` on test class
2. **Container lifecycle**:
   - Use `IAsyncLifetime` for per-test-class isolation
   - Use `ICollectionFixture<T>` + `[Collection("Name")]` to share containers across test classes
3. **Table isolation**: Create unique table names if sharing containers
4. **Documentation**: Add XML comments explaining Docker requirement

Example:
```csharp
/// <summary>
/// Integration tests using Testcontainers.
/// Requires Docker Desktop to be running.
/// </summary>
[Trait("Category", "Integration")]
public class MyIntegrationTests : IAsyncLifetime
{
    private MsSqlContainer? _container;

    public async Task InitializeAsync()
    {
        _container = new MsSqlBuilder().Build();
        await _container.StartAsync();
    }

    public async Task DisposeAsync()
    {
        if (_container != null)
            await _container.DisposeAsync();
    }
}
```

---

## Pull Request Process

### Before Submitting

1. **Create an issue** first for large changes to discuss approach
2. **Fork the repository** and create a feature branch
3. **Write tests** achieving ≥99% line coverage
4. **Update documentation** (XML docs, README if API changed)
5. **Run local tests** and ensure they pass
6. **Follow code style** guidelines above

### PR Checklist

```markdown
- [ ] Tests added/updated achieving ≥95% line coverage, ≥90% branch coverage
- [ ] All error modes tested (FailFast, CollectAndContinue, BestEffort)
- [ ] XML documentation complete for public APIs
- [ ] README updated if API/features changed
- [ ] Package README files updated (src/*/README.md)
- [ ] ConfigureAwait(false) used consistently
- [ ] Nullable reference types handled
- [ ] No breaking changes (or discussed in issue for major version)
- [ ] Benchmarks run (if performance-sensitive change)
- [ ] All CI checks pass (tests, coverage, flaky detection, CodeQL)
```

### PR Guidelines

- **Title**: Use descriptive title (e.g., "Fix race condition in ordered output buffer")
- **Description**: Reference related issue, explain changes, show test results
- **Commits**: Use conventional commits (feat:, fix:, docs:, test:, etc.)
- **Size**: Keep PRs focused and reasonably sized (< 500 lines preferred)

### Review Process

1. **Automated Checks**: CI/CD must pass (tests, coverage, flaky detection, CodeQL)
2. **Code Review**: At least one maintainer approval required
3. **Discussion**: Address feedback promptly and professionally
4. **Merge**: Squash and merge by maintainers

---

## Project Structure

```
Rivulet/
├── src/
│   └── Rivulet.Core/           # Core library
│       ├── AsyncParallelLinq.cs
│       ├── ParallelOptionsRivulet.cs
│       ├── ErrorMode.cs
│       └── ...
├── tests/
│   ├── Rivulet.Core.Tests/     # Unit and integration tests
│   └── Rivulet.Benchmarks/     # Performance benchmarks
├── .github/
│   ├── workflows/              # CI/CD pipelines
│   └── ISSUE_TEMPLATE/         # Issue templates
├── TestResults/                # AI context and test artifacts
├── README.md                   # Project documentation
├── PACKAGE_README.md           # NuGet package description
├── ROADMAP.md                  # Future plans
└── CONTRIBUTING.md             # This file
```

### Key Files

- **Source Code**: `src/Rivulet.Core/`
- **Tests**: `tests/Rivulet.Core.Tests/`
- **Benchmarks**: `tests/Rivulet.Benchmarks/`
- **CI/CD**: `.github/workflows/`
- **AI Context**: `TestResults/START_SESSION_AI.md`

---

## Development Workflow

### Feature Development

```bash
# 1. Create feature branch
git checkout -b feature/my-new-feature

# 2. Make changes and commit
git add .
git commit -m "feat: Add support for XYZ"

# 3. Push and create PR
git push origin feature/my-new-feature
# Open PR on GitHub

# 4. Wait for CI/CD and review
# Address feedback if needed

# 5. Merge via GitHub UI (squash and merge)
```

### Commit Message Format

Use [Conventional Commits](https://www.conventionalcommits.org/):

```
feat: Add adaptive batching support
fix: Resolve race condition in ordered output
docs: Update README with batching examples
test: Add edge case coverage for cancellation
perf: Optimize channel buffer allocation
refactor: Extract retry logic to separate class
ci: Update flaky test detection to 30 iterations
```

---

## Common Pitfalls to Avoid

### ❌ Don't Do This

```csharp
// Unbounded parallelism - resource exhaustion
await Task.WhenAll(items.Select(x => ProcessAsync(x)));

// Forgetting ConfigureAwait - potential deadlocks
await someTask;

// Swallowing exceptions silently
catch (Exception) { /* nothing */ }

// Mutable options
public class Options { public int MaxDegreeOfParallelism { get; set; } }

// Using Task instead of ValueTask in hot paths
public async Task<T> ProcessAsync() { /* ... */ }
```

### ✅ Do This

```csharp
// Bounded parallelism with backpressure
await items.SelectParallelAsync(ProcessAsync, options);

// Always ConfigureAwait in library code
await someTask.ConfigureAwait(false);

// Handle exceptions explicitly, call lifecycle hooks
catch (Exception ex) {
    await OnErrorAsync?.Invoke(idx, ex);
    throw;
}

// Immutable options with init properties
public class Options { public int MaxDegreeOfParallelism { get; init; } }

// Use ValueTask for performance
public async ValueTask<T> ProcessAsync() { /* ... */ }
```

---

## Getting Help

- **Questions**: Open a [GitHub Discussion](https://github.com/Jeffeek/Rivulet/discussions)
- **Bugs**: Use [bug report template](.github/ISSUE_TEMPLATE/bug_report.md)
- **Features**: Use [feature request template](.github/ISSUE_TEMPLATE/feature_request.md)
- **Chat**: [Join our Discord/Slack] (if available)

---

## Recognition

Contributors are recognized in:
- GitHub Contributors list
- Release notes for their contributions
- Special mentions for significant contributions

---

## License

By contributing, you agree that your contributions will be licensed under the same [MIT License](LICENSE) that covers the project.

---

## Additional Resources

- [README.md](README.md) - Project overview and usage
- [START_SESSION_AI.md](TestResults/START_SESSION_AI.md) - Comprehensive AI session guide
- [ROADMAP.md](ROADMAP.md) - Future plans and ecosystem packages
- [.github/workflows/WORKFLOW_README.md](.github/workflows/WORKFLOW_README.md) - CI/CD documentation

---

Thank you for contributing to Rivulet! 🚀
