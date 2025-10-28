---
name: Feature Request
about: Suggest a new feature or enhancement for Rivulet
title: '[FEATURE] '
labels: 'enhancement'
assignees: ''

---

## Problem / Use Case
A clear description of the problem this feature would solve or the use case it enables.

**Example**:
"I need to process items from a message queue with at-least-once semantics, but Rivulet doesn't currently support checkpointing..."

## Proposed Solution

### API Design
How would you like to use this feature? Show your ideal API:

```csharp
// Example usage
var source = GetKafkaMessages();
var options = new ParallelOptionsRivulet
{
    MaxDegreeOfParallelism = 10,
    Persistence = new PersistenceOptions
    {
        CheckpointInterval = TimeSpan.FromSeconds(30),
        Storage = new RedisPersistenceStore("localhost")
    }
};

await source.SelectParallelAsync(async (msg, ct) =>
{
    await ProcessMessageAsync(msg, ct);
    return msg.Id;
}, options);
```

### Behavior
Describe how this feature should behave:
- What should happen when...?
- How should errors be handled?
- What are the performance characteristics?
- Should this be opt-in or opt-out?

## Alternatives Considered

Have you considered other approaches? Why is your proposed solution better?

1. **Alternative 1**: [Description]
   - Pros: ...
   - Cons: ...

2. **Alternative 2**: [Description]
   - Pros: ...
   - Cons: ...

## Workaround (if any)
If you have a workaround for this feature, please share it:

```csharp
// Current workaround (if any)
```

## Impact and Priority

### Who benefits from this feature?
- [ ] All Rivulet users
- [ ] Users in specific scenarios (which ones?)
- [ ] Advanced users only
- [ ] Enterprise/production users

### Priority (from your perspective)
- [ ] Critical - Blocking my project
- [ ] High - Would significantly improve my workflow
- [ ] Medium - Nice to have
- [ ] Low - Future enhancement

### Breaking Changes
- [ ] This feature would require breaking changes
- [ ] This feature can be added without breaking changes
- [ ] Unsure

## Additional Context

### Related Issues/Features
- Related to #...
- Builds on #...
- Blocks #...

### Package Scope
Which package should this feature go in?
- [ ] Rivulet.Core (core functionality)
- [ ] New package: Rivulet.[Name] (optional extension)
- [ ] Unsure

### References
Any relevant documentation, blog posts, or examples from other libraries:
- [Link to similar feature in Library X]
- [Blog post about this pattern]

## Checklist
- [ ] I have searched existing issues/feature requests to avoid duplicates
- [ ] I have provided a clear use case and motivation
- [ ] I have proposed an API design (or described desired behavior)
- [ ] I have considered alternatives and trade-offs
- [ ] I am willing to contribute to this feature (optional but appreciated!)
