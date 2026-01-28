#!/usr/bin/env python3
"""
Rivulet Package Management - Generate all documentation and configuration files.

This script generates all files that reference package information:
- README.md (package list, badges)
- samples/README.md (sample project listings)
- ROADMAP.md (version timeline)

Usage:
    python scripts/generate-all.py [--check] [--verbose]

    --check: Check if generated files would differ (for CI)
    --verbose: Print detailed progress
"""

import sys
import argparse
from pathlib import Path
from typing import List, Tuple
import re

# Fix Windows console encoding for emoji support
if sys.platform == 'win32':
    try:
        import io
        if hasattr(sys.stdout, 'buffer'):
            sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')
        if hasattr(sys.stderr, 'buffer'):
            sys.stderr = io.TextIOWrapper(sys.stderr.buffer, encoding='utf-8')
    except (AttributeError, ValueError):
        pass  # Already wrapped or not needed

# Add scripts directory to path
sys.path.insert(0, str(Path(__file__).parent))

from package_registry import load_registry, PackageRegistry


class FileGenerator:
    """Base class for file generators."""

    def __init__(self, registry: PackageRegistry, verbose: bool = False):
        self.registry = registry
        self.verbose = verbose
        self.repo_root = registry.repo_root

    def log(self, message: str):
        """Print message if verbose mode is enabled."""
        if self.verbose:
            print(message)


class ReadmeGenerator(FileGenerator):
    """Generates README.md package list section."""

    def generate(self) -> str:
        """Generate the README.md content."""
        self.log("Generating README.md package list...")

        readme_path = self.repo_root / 'README.md'
        if not readme_path.exists():
            raise FileNotFoundError(f"README.md not found: {readme_path}")

        with open(readme_path, 'r', encoding='utf-8') as f:
            content = f.read()

        # Generate package list
        package_list = self._generate_package_list()

        # Replace the package list section
        # Look for markers: <!-- PACKAGES_START --> ... <!-- PACKAGES_END -->
        pattern = r'<!-- PACKAGES_START -->.*?<!-- PACKAGES_END -->'
        replacement = f'<!-- PACKAGES_START -->\n{package_list}\n<!-- PACKAGES_END -->'

        if re.search(pattern, content, re.DOTALL):
            new_content = re.sub(pattern, replacement, content, flags=re.DOTALL)
        else:
            # Markers not found - add them
            self.log("  ⚠️  Package markers not found in README.md - add them manually")
            return content

        return new_content

    def _generate_package_list(self) -> str:
        """Generate markdown package list."""
        lines = []

        # Core packages
        lines.append("### Core Packages")
        lines.append("")
        for pkg in self.registry.get_core_packages():
            lines.append(self._format_package(pkg))
            lines.append("")

        # Integration packages
        lines.append("### Integration Packages (v1.4.0 🚧)")
        lines.append("")
        for pkg in self.registry.get_integration_packages():
            lines.append(self._format_package(pkg))
            lines.append("")

        return '\n'.join(lines).rstrip()

    def _format_package(self, pkg: dict) -> str:
        """Format a single package entry."""
        name = pkg['name']
        description = pkg['description']
        nuget_badge = self.registry.get_nuget_badge_url(pkg)
        nuget_url = self.registry.get_nuget_url(pkg)
        downloads_badge = self.registry.get_nuget_downloads_badge_url(pkg)

        status_emoji = "✅" if pkg['status'] == 'released' else "🚧"

        lines = [
            f"#### {status_emoji} [{name}]({nuget_url})",
            f"[![NuGet]({nuget_badge})]({nuget_url}) [![Downloads]({downloads_badge})]({nuget_url})",
            "",
            description,
            "",
            "**Key Features:**"
        ]

        for feature in pkg.get('key_features', []):
            lines.append(f"- {feature}")

        return '\n'.join(lines)


class SamplesReadmeGenerator(FileGenerator):
    """Generates samples/README.md."""

    def generate(self) -> str:
        """Generate samples/README.md content."""
        self.log("Generating samples/README.md...")

        lines = [
            "# Rivulet Samples",
            "",
            "This directory contains complete working examples demonstrating how to use all Rivulet packages in real-world scenarios.",
            "",
            "## Available Samples",
            ""
        ]

        # Generate sample entries
        for i, pkg in enumerate(self.registry.packages, 1):
            if 'sample_path' not in pkg:
                continue

            lines.append(f"### {i}. {pkg['sample_name']}")
            lines.append(f"**Package:** `{pkg['name']}`")
            lines.append("")
            lines.append(f"{pkg['description']}")
            lines.append("")

            if 'features' in pkg:
                for feature in pkg['features']:
                    # Split feature into name and description
                    if ' - ' in feature:
                        feature_name, feature_desc = feature.split(' - ', 1)
                        lines.append(f"- **{feature_name}** - {feature_desc}")
                    else:
                        # Fallback if no separator found
                        lines.append(f"- **{feature}**")
                lines.append("")

            lines.append("**Run:**")
            lines.append("```bash")
            lines.append(f"cd {pkg['sample_name']}")
            lines.append("dotnet run")
            lines.append("```")
            lines.append("")

            if 'key_features' in pkg:
                lines.append("**Key Features:**")
                for feature in pkg['key_features']:
                    lines.append(f"- {feature}")
                lines.append("")

            lines.append("---")
            lines.append("")

        # Add footer
        lines.extend([
            "## Running All Samples",
            "",
            "To build all samples:",
            "```bash",
            "dotnet build",
            "```",
            "",
            "To run a specific sample:",
            "```bash",
            "cd <sample-directory>",
            "dotnet run",
            "```",
            "",
            "## Learning Path",
            "",
            "1. **Start with Rivulet.Console.Sample** to understand core operators",
            "2. **Explore Rivulet.Diagnostics.Sample** for production observability",
            "3. **Review Rivulet.OpenTelemetry.Sample** for distributed tracing",
            "4. **Study Rivulet.Testing.Sample** for testing strategies",
            "5. **Examine Rivulet.Hosting.Sample** for enterprise integration",
            "",
            "## Next Steps",
            "",
            "- Read the [Documentation](https://rivulet2.readthedocs.io)",
            "- Review [ROADMAP.md](../ROADMAP.md) for upcoming features",
            "- Contribute on [GitHub](https://github.com/Jeffeek/Rivulet)",
            "",
            "## Support",
            "",
            "For questions or issues:",
            "- Open an issue on GitHub",
            "- Check existing documentation",
            "- Review test projects for more examples",
            ""
        ])

        return '\n'.join(lines)


class RoadmapGenerator(FileGenerator):
    """Generates ROADMAP.md version sections."""

    def generate(self) -> str:
        """Generate ROADMAP.md content."""
        self.log("Generating ROADMAP.md...")

        roadmap_path = self.repo_root / 'docs' / 'ROADMAP.md'
        if not roadmap_path.exists():
            roadmap_path = self.repo_root / 'ROADMAP.md'

        if not roadmap_path.exists():
            self.log("  [WARN] ROADMAP.md not found - skipping")
            return ""

        with open(roadmap_path, 'r', encoding='utf-8') as f:
            content = f.read()

        # Generate version sections
        version_sections = self._generate_version_sections()

        # Replace version sections
        pattern = r'<!-- VERSIONS_START -->.*?<!-- VERSIONS_END -->'
        replacement = f'<!-- VERSIONS_START -->\n{version_sections}\n<!-- VERSIONS_END -->'

        if re.search(pattern, content, re.DOTALL):
            new_content = re.sub(pattern, replacement, content, flags=re.DOTALL)
        else:
            self.log("  [WARN]  Version markers not found in ROADMAP.md")
            return content

        return new_content

    def _generate_version_sections(self) -> str:
        """Generate version sections."""
        lines = []

        for version_data in self.registry.versions:
            version = version_data['version']
            status = version_data['status']
            packages = self.registry.get_packages_by_version(version)

            if not packages:
                continue

            # Version header
            status_emoji = {
                'released': '✅',
                'in_development': '🚧',
                'planned': '📋'
            }.get(status, '❓')

            lines.append(f"### {status_emoji} {version} - {status.replace('_', ' ').title()}")
            lines.append("")

            # Package list
            for pkg in packages:
                lines.append(f"- **{pkg['name']}** - {pkg['description']}")

            lines.append("")

        return '\n'.join(lines).rstrip()


class ReleaseWorkflowGenerator(FileGenerator):
    """Generates release.yml pack commands."""

    def generate(self) -> str:
        """Generate release.yml pack commands."""
        self.log("Generating .github/workflows/release.yml pack commands...")

        workflow_path = self.repo_root / '.github' / 'workflows' / 'release.yml'
        if not workflow_path.exists():
            self.log("  [WARN]  release.yml not found - skipping")
            return ""

        with open(workflow_path, 'r', encoding='utf-8') as f:
            content = f.read()

        # Generate pack commands
        pack_commands = self._generate_pack_commands()

        # Replace pack commands section
        pattern = r'# PACK_COMMANDS_START.*?# PACK_COMMANDS_END'
        replacement = f'# PACK_COMMANDS_START\n{pack_commands}\n        # PACK_COMMANDS_END'

        if re.search(pattern, content, re.DOTALL):
            new_content = re.sub(pattern, replacement, content, flags=re.DOTALL)
        else:
            self.log("  [WARN]  Pack command markers not found in release.yml")
            return content

        return new_content

    def _generate_pack_commands(self) -> str:
        """Generate dotnet pack commands for all packages."""
        lines = []
        for pkg in self.registry.packages:
            path = pkg['path']
            name = pkg['name']
            line = f"        dotnet pack {path}/{name}.csproj -c Release --output ./artifacts -p:PackageVersion=${{{{ steps.get_version.outputs.version }}}}"
            lines.append(line)
        return '\n'.join(lines)


class NugetActivityMonitorGenerator(FileGenerator):
    """Generates nuget-activity-monitor.yml package matrix."""

    def generate(self) -> str:
        """Generate nuget-activity-monitor.yml package list."""
        self.log("Generating .github/workflows/nuget-activity-monitor.yml package list...")

        workflow_path = self.repo_root / '.github' / 'workflows' / 'nuget-activity-monitor.yml'
        if not workflow_path.exists():
            self.log("  [WARN]  nuget-activity-monitor.yml not found - skipping")
            return ""

        with open(workflow_path, 'r', encoding='utf-8') as f:
            content = f.read()

        # Generate package list
        package_list = self._generate_package_list()

        # Replace package list section
        pattern = r'# PACKAGE_LIST_START.*?# PACKAGE_LIST_END'
        replacement = f'# PACKAGE_LIST_START\n{package_list}\n          # PACKAGE_LIST_END'

        if re.search(pattern, content, re.DOTALL):
            new_content = re.sub(pattern, replacement, content, flags=re.DOTALL)
        else:
            self.log("  [WARN]  Package list markers not found in nuget-activity-monitor.yml")
            return content

        return new_content

    def _generate_package_list(self) -> str:
        """Generate YAML package list."""
        lines = []
        for pkg in self.registry.packages:
            lines.append(f"          - '{pkg['name']}'")
        return '\n'.join(lines)


class DependabotGenerator(FileGenerator):
    """Generates dependabot.yml package directories."""

    def generate(self) -> str:
        """Generate dependabot.yml package directories."""
        self.log("Generating .github/dependabot.yml package directories...")

        dependabot_path = self.repo_root / '.github' / 'dependabot.yml'
        if not dependabot_path.exists():
            self.log("  [WARN]  dependabot.yml not found - skipping")
            return ""

        with open(dependabot_path, 'r', encoding='utf-8') as f:
            content = f.read()

        # Generate package directories
        directories = self._generate_directories()

        # Replace package directories section
        pattern = r'# PACKAGE_DIRECTORIES_START.*?# PACKAGE_DIRECTORIES_END'
        replacement = f'# PACKAGE_DIRECTORIES_START\n{directories}\n  # PACKAGE_DIRECTORIES_END'

        if re.search(pattern, content, re.DOTALL):
            new_content = re.sub(pattern, replacement, content, flags=re.DOTALL)
        else:
            self.log("  [WARN]  Package directory markers not found in dependabot.yml")
            return content

        return new_content

    def _generate_directories(self) -> str:
        """Generate dependabot directory entries."""
        lines = []

        # Source packages
        for pkg in self.registry.packages:
            path = pkg['path']
            lines.append(f"  - package-ecosystem: nuget")
            lines.append(f"    directory: /{path}")
            lines.append(f"    schedule:")
            lines.append(f"      interval: weekly")
            lines.append("")

        # Test packages from packages.yml
        test_paths_from_packages = set()
        for pkg in self.registry.packages:
            test_path = pkg.get('test_path')
            if test_path:
                test_paths_from_packages.add(test_path)
                lines.append(f"  - package-ecosystem: nuget")
                lines.append(f"    directory: /{test_path}")
                lines.append(f"    schedule:")
                lines.append(f"      interval: weekly")
                lines.append("")

        # Auto-discover additional test directories (e.g., Benchmarks)
        tests_dir = self.repo_root / 'tests'
        if tests_dir.exists():
            for test_dir in sorted(tests_dir.iterdir()):
                if test_dir.is_dir():
                    # Check if it has a .csproj file
                    has_csproj = any(test_dir.glob('*.csproj'))
                    if has_csproj:
                        # Calculate relative path
                        rel_path = f"tests/{test_dir.name}"
                        # Only add if not already in packages.yml
                        if rel_path not in test_paths_from_packages:
                            lines.append(f"  - package-ecosystem: nuget")
                            lines.append(f"    directory: /{rel_path}")
                            lines.append(f"    schedule:")
                            lines.append(f"      interval: weekly")
                            lines.append("")

        # Remove trailing empty line
        if lines and lines[-1] == "":
            lines.pop()

        return '\n'.join(lines)

def generate_all(check_only: bool = False, verbose: bool = False) -> int:
    """
    Generate all files.

    Args:
        check_only: Only check if files would change (for CI)
        verbose: Print detailed progress

    Returns:
        Exit code (0 = success, 1 = error or changes detected)
    """
    try:
        # Load registry
        registry = load_registry()
        if verbose:
            print(f"[OK] Loaded {len(registry.packages)} packages")
            print()

        # Validate registry first
        errors = registry.validate(verbose=verbose)
        if errors:
            print("[ERR] Package registry validation failed:")
            for error in errors:
                print(f"  - {error}")
            return 1

        if verbose:
            print()

        # Generate files
        generators = [
            ('README.md', ReadmeGenerator(registry, verbose)),
            ('samples/README.md', SamplesReadmeGenerator(registry, verbose)),
            ('ROADMAP.md', RoadmapGenerator(registry, verbose)),
            ('.github/workflows/release.yml', ReleaseWorkflowGenerator(registry, verbose)),
            ('.github/workflows/nuget-activity-monitor.yml', NugetActivityMonitorGenerator(registry, verbose)),
            ('.github/dependabot.yml', DependabotGenerator(registry, verbose)),
        ]

        changes_detected = False

        for file_desc, generator in generators:
            try:
                new_content = generator.generate()
                if not new_content:
                    continue  # Generator skipped this file

                # Determine file path
                if file_desc == 'README.md':
                    file_path = registry.repo_root / 'README.md'
                elif file_desc == 'samples/README.md':
                    file_path = registry.repo_root / 'samples' / 'README.md'
                elif file_desc == 'ROADMAP.md':
                    file_path = registry.repo_root / 'docs' / 'ROADMAP.md'
                    if not file_path.exists():
                        file_path = registry.repo_root / 'ROADMAP.md'
                elif file_desc == '.github/workflows/release.yml':
                    file_path = registry.repo_root / '.github' / 'workflows' / 'release.yml'
                elif file_desc == '.github/workflows/nuget-activity-monitor.yml':
                    file_path = registry.repo_root / '.github' / 'workflows' / 'nuget-activity-monitor.yml'
                elif file_desc == '.github/dependabot.yml':
                    file_path = registry.repo_root / '.github' / 'dependabot.yml'

                # Check if changed
                if file_path.exists():
                    with open(file_path, 'r', encoding='utf-8') as f:
                        old_content = f.read()

                    if old_content != new_content:
                        if check_only:
                            print(f"[ERR] {file_desc} needs regeneration")
                            changes_detected = True
                        else:
                            with open(file_path, 'w', encoding='utf-8') as f:
                                f.write(new_content)
                            print(f"[OK] Updated {file_desc}")
                    else:
                        if verbose:
                            print(f"[OK] {file_desc} is up to date")
                else:
                    if verbose:
                        print(f"[WARN]  {file_desc} not found")

            except Exception as e:
                print(f"[ERR] Error generating {file_desc}: {e}")
                if verbose:
                    import traceback
                    traceback.print_exc()
                return 1

        if check_only and changes_detected:
            print()
            print("[ERR] Some files need regeneration. Run: ./scripts/update-all.sh")
            return 1

        if verbose:
            print()
            print("[OK] All files generated successfully!")

        return 0

    except Exception as e:
        print(f"[ERR] Error: {e}")
        if verbose:
            import traceback
            traceback.print_exc()
        return 1


def main():
    parser = argparse.ArgumentParser(
        description='Generate all documentation and configuration files from packages.yml'
    )
    parser.add_argument(
        '--check',
        action='store_true',
        help='Check if generated files would differ (for CI)'
    )
    parser.add_argument(
        '--verbose', '-v',
        action='store_true',
        help='Print detailed progress'
    )

    args = parser.parse_args()

    exit_code = generate_all(check_only=args.check, verbose=args.verbose)
    sys.exit(exit_code)


if __name__ == '__main__':
    main()
