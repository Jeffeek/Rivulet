# Security Policy

## Supported Versions

The following versions of Rivulet are currently supported with security updates:

| Version | Supported          | End of Support |
| ------- | ------------------ | -------------- |
| 1.2.x   | :white_check_mark: | Active development |
| 1.1.x   | :white_check_mark: | Until 1.3.0 release |
| 1.0.x   | :white_check_mark: | Until 1.3.0 release |
| 1.0.0-alpha| :x:                | No longer supported |

Security patches are provided for the current major.minor version (1.2.x) and the previous two minor versions (1.1.x, 1.0.x).

## Reporting a Vulnerability

**DO NOT** report security vulnerabilities through public GitHub issues.

### How to Report

We use GitHub's private vulnerability reporting feature:

1. Go to the [Security tab](https://github.com/Jeffeek/Rivulet/security)
2. Click **"Report a vulnerability"**
3. Fill in the details (see below for what to include)

**Alternative:** If private reporting is unavailable, email the maintainers directly via GitHub profile contact methods.

### What to Include

Please provide the following information:

- **Type of vulnerability** (e.g., DoS, memory corruption, information disclosure, remote code execution)
- **Affected versions** (e.g., 1.2.0, 1.1.5, all versions)
- **Affected components** (e.g., Rivulet.Core, Rivulet.Diagnostics)
- **Full paths** of affected source files (if known)
- **Step-by-step reproduction instructions**
- **Proof-of-concept or exploit code** (if possible)
- **Impact assessment** (what can an attacker do?)
- **Suggested fix** (if you have one)

### Response Timeline

| Stage | Timeline |
|-------|----------|
| **Initial acknowledgment** | Within 48 hours (2 business days) |
| **Preliminary assessment** | Within 7 days |
| **Status updates** | Every 7-14 days until resolved |
| **Fix development** | 30-90 days (depending on severity) |
| **Coordinated disclosure** | After fix is released |

### Severity Classification

We follow the [CVSS v3.1](https://www.first.org/cvss/v3.1/specification-document) scoring system:

- **Critical (9.0-10.0)**: Immediate action, patch within 7 days
- **High (7.0-8.9)**: High priority, patch within 30 days
- **Medium (4.0-6.9)**: Normal priority, patch within 60 days
- **Low (0.1-3.9)**: Low priority, patch within 90 days

### What to Expect

**If accepted:**
- We'll work with you on coordinated disclosure timing
- You'll be credited in the security advisory (if desired)
- A CVE will be requested for trackable vulnerabilities
- A security patch will be released
- A security advisory will be published

**If declined:**
- We'll explain why the issue doesn't qualify as a security vulnerability
- Alternative solutions or workarounds may be suggested
- The issue may be converted to a regular bug report (with your permission)

### Disclosure Policy

We follow **coordinated disclosure**:
- We'll work with you to agree on a disclosure date
- Typically 90 days after initial report (adjustable)
- Public disclosure occurs after a fix is released
- Credit is given to the reporter (unless anonymity is requested)

### Out of Scope

The following are **not** considered security vulnerabilities:

- DoS via resource exhaustion when using unbounded parallelism (use `MaxDegreeOfParallelism`)
- Issues in third-party dependencies (report to upstream)
- Issues requiring physical access to the machine
- Social engineering attacks
- Issues in example/sample code (not production code)

### Security Best Practices

When using Rivulet in production:

1. **Always set `MaxDegreeOfParallelism`** to prevent resource exhaustion
2. **Use timeouts** (`PerItemTimeout`) for untrusted operations
3. **Validate inputs** before parallel processing
4. **Enable circuit breakers** for external service calls
5. **Monitor metrics** to detect abnormal behavior
6. **Keep dependencies updated** (`dotnet list package --outdated`)

### Hall of Fame

Security researchers who have responsibly disclosed vulnerabilities:

*None yet - be the first!*

### Questions?

If you have questions about this security policy:
- Open a [GitHub Discussion](https://github.com/Jeffeek/Rivulet/discussions)
- Contact the maintainers via GitHub profile

Thank you for helping keep Rivulet secure! 🔒
