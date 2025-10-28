---
name: Bug Report
about: Report a bug to help us improve Rivulet
title: '[BUG] '
labels: 'bug'
assignees: ''

---

## Bug Description
A clear and concise description of the bug.

## To Reproduce
Minimal code example that reproduces the issue:

```csharp
var source = Enumerable.Range(1, 100);
var options = new ParallelOptionsRivulet { MaxDegreeOfParallelism = 10 };

var results = await source.SelectParallelAsync(async (x, ct) =>
{
    // Your code that triggers the bug
    return x * 2;
}, options);
```

**Steps**:
1. Run the code above
2. Observe the behavior
3. See error/unexpected result

## Expected Behavior
A clear description of what you expected to happen.

## Actual Behavior
What actually happened instead.

## Environment
**Rivulet Version**: [e.g., 1.1.0]
**Target Framework**: [e.g., net9.0, net8.0]
**.NET SDK Version**: [e.g., 9.0.100]
**Operating System**: [e.g., Windows 11, Ubuntu 22.04, macOS 14]
**Runtime**: [e.g., CoreCLR, NativeAOT]

## Stack Trace
If applicable, include the full stack trace:

```
System.InvalidOperationException: ...
   at Rivulet.Core.AsyncParallelLinq.SelectParallelAsync[...]
   at MyApp.ProcessAsync(...)
```

## Additional Context

### Configuration
If using complex options, include your full configuration:

```csharp
var options = new ParallelOptionsRivulet
{
    MaxDegreeOfParallelism = 32,
    ErrorMode = ErrorMode.FailFast,
    MaxRetries = 3,
    // ... other options
};
```

### Frequency
- [ ] Happens every time
- [ ] Happens intermittently (race condition?)
- [ ] Happens only under specific conditions

### Impact
- [ ] Blocks development
- [ ] Production issue
- [ ] Performance degradation
- [ ] Minor inconvenience

### Workaround
If you found a workaround, please share it here to help others.

## Checklist
- [ ] I have searched existing issues to ensure this is not a duplicate
- [ ] I have provided a minimal code example that reproduces the issue
- [ ] I have included my environment details (.NET version, OS, Rivulet version)
- [ ] I have included stack traces if applicable
