# Rivulet Package Management System

## 📋 Overview

This document describes the **Rivulet Package Management System** - a centralized approach to managing package information across the entire repository.

### The Problem

When adding a new package to Rivulet, you previously had to update information in multiple places:
- ✏️ `README.md` - Package list and badges
- ✏️ `samples/README.md` - Sample project listings
- ✏️ `docs/ROADMAP.md` - Version timeline
- ✏️ `.github/workflows/ci.yml` - Build matrices
- ✏️ `mkdocs.yml` - Documentation navigation
- ✏️ And potentially more...

This was error-prone, tedious, and easy to forget.

### The Solution

**Single Source of Truth:** The `packages.yml` file at the repository root is now the **only place** where you define package information.

All documentation, workflows, and scripts are **automatically generated** from `packages.yml`.

---

## 🚀 Quick Start

### Adding a New Package

1. **Add your package to `packages.yml`:**

```yaml
- name: Rivulet.Example
  id: example
  category: integration
  version: 1.4.0
  status: in_development
  nuget_id: Rivulet.Example
  description: Example package description
  path: src/Rivulet.Example
  test_path: tests/Rivulet.Example.Tests
  sample_path: samples/Rivulet.Example.Sample
  sample_name: Rivulet.Example.Sample
  features:
    - Feature1 - Description
    - Feature2 - Description
  key_features:
    - Key feature 1
    - Key feature 2
  dependencies:
    - Rivulet.Core
  targets:
    - net8.0
    - net9.0
```

2. **Run the update script:**

**Linux/macOS:**
```bash
./scripts/UpdateAll/update-all.sh
```

**Windows (PowerShell):**
```powershell
.\scripts\UpdateAll\update-all.ps1
```

**Windows (Command Prompt):**
```cmd
pwsh -File .\scripts\UpdateAll\update-all.ps1
```

3. **Review the changes:**

```bash
git diff
```

4. **Commit the changes:**

```bash
git add packages.yml README.md samples/README.md docs/ROADMAP.md
git commit -m "Add Rivulet.Example package"
```

**That's it!** All documentation and configuration files are now updated.

---

## 📚 File Structure

```
Rivulet/
├── packages.yml                    # 📋 SINGLE SOURCE OF TRUTH
├── PACKAGE_MANAGEMENT.md           # 📖 This documentation
│
├── scripts/
│   ├── package_registry.py         # 🔧 Core registry loader
│   ├── generate-all.py             # 🔧 Main generation script
│   ├── update-all.sh               # 🚀 Master script (Linux/macOS)
│   └── update-all.ps1              # 🚀 Master script (Windows)
│
├── README.md                       # ✅ GENERATED (package list)
├── samples/README.md               # ✅ GENERATED (sample listings)
└── docs/ROADMAP.md                 # ✅ GENERATED (version sections)
```

---

## 📝 packages.yml Schema

### Required Fields

Every package **must** have these fields:

| Field | Type | Description | Example |
|-------|------|-------------|---------|
| `name` | string | Full package name | `Rivulet.Http` |
| `id` | string | Short unique identifier | `http` |
| `category` | string | Package category | `core` or `integration` |
| `version` | string | Target version | `1.3.0` |
| `status` | string | Development status | `released`, `in_development`, `planned` |
| `nuget_id` | string | NuGet package ID | `Rivulet.Http` |
| `description` | string | One-line description | `Parallel HTTP operations...` |
| `path` | string | Source directory | `src/Rivulet.Http` |
| `test_path` | string | Test directory | `tests/Rivulet.Http.Tests` |

### Optional Fields

| Field | Type | Description | Example |
|-------|------|-------------|---------|
| `sample_path` | string | Sample directory | `samples/Rivulet.Http.Sample` |
| `sample_name` | string | Sample project name | `Rivulet.Http.Sample` |
| `features` | list | Feature list | `["GetParallelAsync - Fetch URLs"]` |
| `key_features` | list | Key features | `["HttpClientFactory integration"]` |
| `dependencies` | list | Package dependencies | `["Rivulet.Core"]` |
| `targets` | list | Target frameworks | `["net8.0", "net9.0"]` |

### Package Status Values

- **`released`** - Package is released and available on NuGet (shows ✅ badge)
- **`in_development`** - Package is actively being developed (shows 🚧 badge)
- **`planned`** - Package is planned for future development (shows 📋 badge)

### Package Categories

- **`core`** - Core packages providing essential functionality
- **`integration`** - Packages integrating with external systems/libraries

---

## 🔧 Generated Files

### README.md

**Generated Section:** Package list with badges and features

**Markers:**
```markdown
<!-- PACKAGES_START -->
...generated content...
<!-- PACKAGES_END -->
```

**What's Generated:**
- Package list organized by category
- NuGet badges (version, downloads)
- Feature lists
- Status indicators (✅ released, 🚧 in development)

---

### samples/README.md

**Fully Generated:** Entire file is regenerated

**What's Generated:**
- Sample project listings
- Run instructions
- Feature highlights
- Learning path

---

### docs/ROADMAP.md

**Generated Section:** Version timeline and package lists

**Markers:**
```markdown
<!-- VERSIONS_START -->
...generated content...
<!-- VERSIONS_END -->
```

**What's Generated:**
- Version sections with packages
- Status indicators
- Package descriptions

---

## 🤖 CI/CD Integration

### Validation in CI

A GitHub Actions workflow validates that generated files are up-to-date:

```yaml
- name: Validate package registry
  run: python scripts/UpdateAll/generate-all.py --check
```

If generated files are out of date, the CI build will **fail** and remind you to run `./scripts/UpdateAll/update-all.sh`.

---

## 🛠️ Scripts Reference

### update-all.sh / update-all.ps1

**Purpose:** Regenerate all files from `packages.yml`

**Usage:**
```bash
# Linux/macOS
./scripts/UpdateAll/update-all.sh

# Windows (PowerShell)
.\scripts\UpdateAll\update-all.ps1 [-Verbose]
```

**What It Does:**
1. Validates `packages.yml` schema and structure
2. Checks that all referenced paths exist
3. Validates dependencies
4. Generates all documentation files
5. Reports what was updated

**Exit Codes:**
- `0` - Success
- `1` - Validation or generation error

---

### package_registry.py

**Purpose:** Core module for loading and validating `packages.yml`

**Usage:**
```bash
python scripts/package_registry.py
```

**What It Does:**
- Loads and parses `packages.yml`
- Validates schema and required fields
- Checks for duplicate IDs/names
- Verifies all paths exist
- Validates dependencies

Can be imported by other scripts:
```python
from package_registry import load_registry

registry = load_registry()
packages = registry.get_core_packages()
```

---

### generate-all.py

**Purpose:** Generate all documentation files

**Usage:**
```bash
# Generate all files
python scripts/UpdateAll/generate-all.py [--verbose]

# Check if files need regeneration (for CI)
python scripts/UpdateAll/generate-all.py --check
```

**Options:**
- `--check` - Only check if files would change (for CI)
- `--verbose` - Print detailed progress

**Exit Codes:**
- `0` - Success (or no changes needed)
- `1` - Error or changes detected (in check mode)

---

## 📋 Workflows

### Adding a New Package

**Step 1:** Create the package structure
```bash
# Create directories
mkdir -p src/Rivulet.Example
mkdir -p tests/Rivulet.Example.Tests
mkdir -p samples/Rivulet.Example.Sample

# Create .csproj files
dotnet new classlib -o src/Rivulet.Example -n Rivulet.Example
dotnet new xunit -o tests/Rivulet.Example.Tests -n Rivulet.Example.Tests
dotnet new console -o samples/Rivulet.Example.Sample -n Rivulet.Example.Sample

# Add to solution
dotnet sln add src/Rivulet.Example/Rivulet.Example.csproj
dotnet sln add tests/Rivulet.Example.Tests/Rivulet.Example.Tests.csproj
dotnet sln add samples/Rivulet.Example.Sample/Rivulet.Example.Sample.csproj
```

**Step 2:** Add package to `packages.yml`

Edit `packages.yml` and add your package entry (see schema above).

**Step 3:** Regenerate all files
```bash
./scripts/UpdateAll/update-all.sh
```

**Step 4:** Review and commit
```bash
git diff
git add packages.yml README.md samples/README.md docs/ROADMAP.md .invisible/START_SESSION_AI.md
git commit -m "Add Rivulet.Example package"
```

---

### Updating Package Information

**Step 1:** Edit `packages.yml`

Update the package entry (e.g., add features, change description).

**Step 2:** Regenerate
```bash
./scripts/UpdateAll/update-all.sh
```

**Step 3:** Review and commit
```bash
git diff
git add packages.yml README.md samples/README.md docs/ROADMAP.md
git commit -m "Update Rivulet.Example package info"
```

---

### Removing a Package

**Step 1:** Remove from `packages.yml`

Delete the package entry.

**Step 2:** Regenerate
```bash
./scripts/UpdateAll/update-all.sh
```

**Step 3:** Remove directories
```bash
rm -rf src/Rivulet.Example
rm -rf tests/Rivulet.Example.Tests
rm -rf samples/Rivulet.Example.Sample
dotnet sln remove src/Rivulet.Example/Rivulet.Example.csproj
dotnet sln remove tests/Rivulet.Example.Tests/Rivulet.Example.Tests.csproj
dotnet sln remove samples/Rivulet.Example.Sample/Rivulet.Example.Sample.csproj
```

**Step 4:** Commit
```bash
git add -A
git commit -m "Remove Rivulet.Example package"
```

---

## ✅ Validation

### What Gets Validated

The package registry validates:

1. **Schema Validation**
   - Required fields are present
   - Field types are correct

2. **Uniqueness**
   - No duplicate package IDs
   - No duplicate package names

3. **Path Validation**
   - All `path` entries exist
   - `.csproj` files exist in each path
   - All `test_path` entries exist
   - All `sample_path` entries exist (if specified)

4. **Dependency Validation**
   - All dependencies reference valid packages
   - No circular dependencies

5. **Category Validation**
   - Categories are defined in the `categories` section

### Running Validation Manually

```bash
python scripts/package_registry.py
```

This is automatically run by `update-all.sh` before generation.

---

## 🎯 Best Practices

### 1. Always Run update-all After Editing packages.yml

```bash
# Edit packages.yml
vim packages.yml

# Regenerate
./scripts/UpdateAll/update-all.sh

# Review
git diff
```

### 2. Commit packages.yml and Generated Files Together

```bash
git add packages.yml README.md samples/README.md docs/ROADMAP.md
git commit -m "Update package information"
```

### 3. Use Descriptive Package IDs

- ✅ Good: `http`, `sql-postgresql`, `diagnostics-otel`
- ❌ Bad: `pkg1`, `new`, `temp`

### 4. Keep Descriptions Concise

Descriptions should be one line, under 100 characters.

- ✅ Good: `Parallel HTTP operations with HttpClientFactory integration`
- ❌ Bad: `This package provides a comprehensive set of utilities for performing HTTP operations in parallel using HttpClient with support for...`

### 5. Use Semantic Versioning

Follow semantic versioning for the `version` field:
- Major: `1.0.0` → `2.0.0` (breaking changes)
- Minor: `1.0.0` → `1.1.0` (new features, backward compatible)
- Patch: `1.0.0` → `1.0.1` (bug fixes)

---

## 🔍 Troubleshooting

### PyYAML Not Found

**Error:**
```
ModuleNotFoundError: No module named 'yaml'
```

**Solution:**
```bash
pip install pyyaml
```

---

### Python Not Found

**Error:**
```
python: command not found
```

**Solution:**
- Install Python 3.8 or later
- On some systems, use `python3` instead of `python`

---

### Package Path Validation Failed

**Error:**
```
❌ Rivulet.Example: Package path does not exist: src/Rivulet.Example
```

**Solution:**
- Create the directory: `mkdir -p src/Rivulet.Example`
- Or fix the path in `packages.yml`

---

### Generated Files Out of Date (CI)

**Error in CI:**
```
❌ README.md needs regeneration
```

**Solution:**
```bash
./scripts/UpdateAll/update-all.sh
git add README.md samples/README.md docs/ROADMAP.md
git commit -m "Update generated files"
git push
```

---

## 📖 Additional Resources

- **Repository:** [https://github.com/your-org/Rivulet](https://github.com/your-org/Rivulet)
- **Documentation:** [https://rivulet.readthedocs.io](https://rivulet.readthedocs.io)
- **Issues:** [https://github.com/your-org/Rivulet/issues](https://github.com/your-org/Rivulet/issues)

---

## 🤝 Contributing

When contributing new packages, please follow this workflow:

1. Create the package structure (src, tests, samples)
2. Add package to `packages.yml`
3. Run `./scripts/UpdateAll/update-all.sh`
4. Commit all changes together
5. Submit a pull request

The CI workflow will validate that `packages.yml` and generated files are in sync.

---

## 📝 License

This package management system is part of the Rivulet project and is licensed under the MIT License.

---

**Last Updated:** 2025-11-24
**Version:** 1.0.0
