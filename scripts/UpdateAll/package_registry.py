#!/usr/bin/env python3
"""
Rivulet Package Registry - Core module for loading and validating packages.yml

Auto-derives most fields from the codebase:
  - nuget_id, path, test_path, sample_path, sample_name: from 'name'
  - description, features, key_features: from README.md markers
  - dependencies: from .csproj (ProjectReference + PackageReference)
  - targets: from defaults, validated against .csproj TargetFrameworks
"""

import os
import re
import sys
import xml.etree.ElementTree as ET
from pathlib import Path
from typing import Dict, List, Optional, Any
import yaml


class PackageRegistry:
    """
    Loads packages.yml and auto-derives all fields from the codebase.
    """

    def __init__(self, registry_path: Optional[Path] = None):
        if registry_path is None:
            registry_path = self._find_registry()

        self.registry_path = registry_path
        self.repo_root = registry_path.parent

        with open(registry_path, 'r', encoding='utf-8') as f:
            self.data = yaml.safe_load(f)

        self.metadata = self.data.get('metadata', {})
        self.versions = self.data.get('versions', [])
        self.badges = self.data.get('badges', {})
        self.links = self.data.get('links', {})
        self.defaults = self.data.get('defaults', {})

        # Enrich packages with derived fields (guard against malformed entries)
        self.packages = []
        for pkg in self.data.get('packages', []):
            if not pkg.get('name') or not pkg.get('id'):
                raise ValueError(
                    f"Package entry missing 'name' or 'id': {pkg}. "
                    "Every entry in packages.yml must have at least name, id, version, status."
                )
            self.packages.append(self._enrich(pkg))

        # Create lookup maps
        self._package_by_id = {pkg['id']: pkg for pkg in self.packages}
        self._package_by_name = {pkg['name']: pkg for pkg in self.packages}

    def _find_registry(self) -> Path:
        """Find packages.yml by searching from current directory to repo root."""
        current = Path.cwd()
        for _ in range(10):
            candidate = current / 'packages.yml'
            if candidate.exists():
                return candidate
            if (current / '.git').exists():
                raise FileNotFoundError(f"packages.yml not found in repository root: {current}")
            parent = current.parent
            if parent == current:
                break
            current = parent
        raise FileNotFoundError("packages.yml not found. Make sure you're in the Rivulet repository.")

    # -------------------------------------------------------------------------
    # Field derivation
    # -------------------------------------------------------------------------

    def _enrich(self, pkg: dict) -> dict:
        """Enrich a minimal package entry with all derived fields."""
        name = pkg['name']

        # Convention-based paths
        pkg['nuget_id'] = name
        pkg['path'] = f"src/{name}"
        pkg['test_path'] = f"tests/{name}.Tests"
        pkg['sample_name'] = f"{name}.Sample"
        pkg['sample_path'] = f"samples/{name}.Sample"
        pkg['targets'] = self.defaults.get('targets', ['net8.0', 'net9.0'])

        # Derived from codebase files
        pkg['description'] = self._parse_readme_marker(name, 'DESCRIPTION')
        pkg['key_features'] = self._parse_readme_marker_list(name, 'KEY_FEATURES')
        pkg['features'] = self._parse_readme_marker_list(name, 'FEATURES')

        # Dependencies from .csproj
        deps = self._parse_csproj_deps(name)
        pkg['dependencies'] = deps.get('project_refs', [])
        pkg['external_dependencies'] = deps.get('package_refs', [])

        return pkg

    def _parse_readme_marker(self, name: str, marker: str) -> str:
        """Extract text between <!-- {MARKER}_START --> and <!-- {MARKER}_END --> from README."""
        readme_path = self.repo_root / 'src' / name / 'README.md'
        if not readme_path.exists():
            return ''
        content = readme_path.read_text(encoding='utf-8')
        pattern = rf'<!-- {marker}_START -->\s*\n(.*?)\n\s*<!-- {marker}_END -->'
        match = re.search(pattern, content, re.DOTALL)
        if not match:
            return ''
        # For DESCRIPTION: strip markdown bold markers and leading/trailing whitespace
        text = match.group(1).strip()
        if marker == 'DESCRIPTION':
            # Remove **bold** markers and collapse to single line
            text = text.replace('**', '').strip()
        return text

    def _parse_readme_marker_list(self, name: str, marker: str) -> List[str]:
        """Extract bullet list between markers from README."""
        readme_path = self.repo_root / 'src' / name / 'README.md'
        if not readme_path.exists():
            return []
        content = readme_path.read_text(encoding='utf-8')
        pattern = rf'<!-- {marker}_START -->\s*\n(.*?)\n\s*<!-- {marker}_END -->'
        match = re.search(pattern, content, re.DOTALL)
        if not match:
            return []

        items = []
        for line in match.group(1).strip().split('\n'):
            line = line.strip()
            if line.startswith('- '):
                item = line[2:].strip()
                # Strip **bold** from feature names: "**Name** - Desc" -> "Name - Desc"
                item = re.sub(r'\*\*([^*]+)\*\*', r'\1', item)
                items.append(item)
        return items

    def _parse_csproj_deps(self, name: str) -> Dict[str, List[str]]:
        """Parse ProjectReference and PackageReference from .csproj."""
        csproj_path = self.repo_root / 'src' / name / f'{name}.csproj'
        result = {'project_refs': [], 'package_refs': []}
        if not csproj_path.exists():
            return result

        try:
            tree = ET.parse(csproj_path)
            root = tree.getroot()

            for ref in root.iter('ProjectReference'):
                include = ref.get('Include', '')
                # Normalize backslashes for cross-platform: ..\Rivulet.IO\Rivulet.IO.csproj
                normalized = include.replace('\\', '/')
                proj_file = Path(normalized).stem  # "Rivulet.IO"
                if proj_file.startswith('Rivulet.'):
                    result['project_refs'].append(proj_file)

            for ref in root.iter('PackageReference'):
                include = ref.get('Include', '')
                if include:
                    result['package_refs'].append(include)

        except ET.ParseError as e:
            raise ValueError(f"Failed to parse {csproj_path}: {e}")

        return result

    # -------------------------------------------------------------------------
    # Lookups
    # -------------------------------------------------------------------------

    def get_package(self, identifier: str) -> Optional[Dict[str, Any]]:
        """Get package by ID or name."""
        return self._package_by_id.get(identifier) or self._package_by_name.get(identifier)

    def get_packages_by_status(self, status: str) -> List[Dict[str, Any]]:
        return [pkg for pkg in self.packages if pkg.get('status') == status]

    def get_packages_by_version(self, version: str) -> List[Dict[str, Any]]:
        version_data = next((v for v in self.versions if v['version'] == version), None)
        if not version_data:
            return []
        package_ids = version_data.get('packages', [])
        return [self._package_by_id[pid] for pid in package_ids if pid in self._package_by_id]

    def get_released_packages(self) -> List[Dict[str, Any]]:
        return self.get_packages_by_status('released')

    def get_in_development_packages(self) -> List[Dict[str, Any]]:
        return self.get_packages_by_status('in_development')

    def get_nuget_badge_url(self, package: Dict[str, Any]) -> str:
        return self.badges['nuget_badge'].format(nuget_id=package['nuget_id'])

    def get_nuget_url(self, package: Dict[str, Any]) -> str:
        return self.badges['nuget_url'].format(nuget_id=package['nuget_id'])

    def get_nuget_downloads_badge_url(self, package: Dict[str, Any]) -> str:
        return self.badges['nuget_downloads'].format(nuget_id=package['nuget_id'])

    # -------------------------------------------------------------------------
    # Validation
    # -------------------------------------------------------------------------

    def validate(self, verbose: bool = False) -> List[str]:
        """Validate the package registry and derived fields."""
        errors = []
        if verbose:
            print("Validating package registry...")

        # Duplicate IDs
        ids = [pkg['id'] for pkg in self.packages]
        dupes = set(i for i in ids if ids.count(i) > 1)
        if dupes:
            errors.append(f"Duplicate package IDs: {dupes}")

        # Duplicate names
        names = [pkg['name'] for pkg in self.packages]
        dupes = set(n for n in names if names.count(n) > 1)
        if dupes:
            errors.append(f"Duplicate package names: {dupes}")

        for pkg in self.packages:
            errors.extend(self._validate_package(pkg, verbose))

        if verbose:
            if errors:
                print(f"[ERR] Validation failed with {len(errors)} errors")
            else:
                print("[OK] Validation passed!")

        return errors

    def _validate_package(self, pkg: Dict[str, Any], verbose: bool) -> List[str]:
        errors = []
        name = pkg.get('name', 'Unknown')
        if verbose:
            print(f"  Validating {name}...")

        # Required YAML fields
        for field in ['name', 'id', 'version', 'status']:
            if field not in pkg or not pkg[field]:
                errors.append(f"{name}: Missing required field '{field}'")

        # Source path must exist
        src_path = self.repo_root / pkg['path']
        if not src_path.exists():
            errors.append(f"{name}: Source path does not exist: {pkg['path']}")
        else:
            csproj = src_path / f"{name}.csproj"
            if not csproj.exists():
                errors.append(f"{name}: .csproj not found: {csproj}")

        # Test path must exist
        test_path = self.repo_root / pkg['test_path']
        if not test_path.exists():
            errors.append(f"{name}: Test path does not exist: {pkg['test_path']}")

        # Sample path (optional — warn if missing)
        sample_path = self.repo_root / pkg['sample_path']
        if not sample_path.exists():
            if verbose:
                print(f"    [WARN] No sample project: {pkg['sample_path']}")

        # README markers must be present
        if not pkg.get('description'):
            errors.append(f"{name}: No DESCRIPTION markers found in README.md")
        if not pkg.get('key_features'):
            errors.append(f"{name}: No KEY_FEATURES markers found in README.md")
        if not pkg.get('features'):
            errors.append(f"{name}: No FEATURES markers found in README.md")

        # Validate dependencies reference known packages
        for dep in pkg.get('dependencies', []):
            if not self.get_package(dep):
                errors.append(f"{name}: Unknown dependency: {dep}")

        # Validate targets match global defaults
        expected_targets = set(self.defaults.get('targets', []))
        if expected_targets:
            csproj_path = self.repo_root / 'src' / name / f'{name}.csproj'
            if csproj_path.exists():
                actual_targets = self._read_csproj_targets(csproj_path)
                if actual_targets and actual_targets != expected_targets:
                    errors.append(
                        f"{name}: TargetFrameworks mismatch — "
                        f"expected {sorted(expected_targets)}, "
                        f"got {sorted(actual_targets)}"
                    )

        return errors

    def _read_csproj_targets(self, csproj_path: Path) -> set:
        """Read TargetFrameworks from .csproj."""
        try:
            tree = ET.parse(csproj_path)
            root = tree.getroot()
            for elem in root.iter('TargetFrameworks'):
                return set(t.strip() for t in elem.text.split(';') if t.strip())
            for elem in root.iter('TargetFramework'):
                return {elem.text.strip()}
        except ET.ParseError as e:
            raise ValueError(f"Failed to parse {csproj_path}: {e}")
        return set()


def load_registry(registry_path: Optional[Path] = None) -> PackageRegistry:
    """Convenience function to load the package registry."""
    return PackageRegistry(registry_path)


if __name__ == '__main__':
    # Fix Windows console encoding for emoji support
    if sys.platform == 'win32':
        try:
            import io
            if hasattr(sys.stdout, 'buffer'):
                sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')
            if hasattr(sys.stderr, 'buffer'):
                sys.stderr = io.TextIOWrapper(sys.stderr.buffer, encoding='utf-8')
        except (AttributeError, ValueError):
            pass

    try:
        registry = load_registry()
        print(f"[OK] Loaded {len(registry.packages)} packages from {registry.registry_path}")
        print(f"   Repository root: {registry.repo_root}")
        print()

        # Show derived fields for first package as sample
        if registry.packages:
            pkg = registry.packages[0]
            print(f"   Sample derived fields for {pkg['name']}:")
            print(f"     nuget_id:     {pkg['nuget_id']}")
            print(f"     path:         {pkg['path']}")
            print(f"     test_path:    {pkg['test_path']}")
            print(f"     sample_name:  {pkg['sample_name']}")
            print(f"     description:  {pkg['description'][:80]}...")
            print(f"     dependencies: {pkg['dependencies']}")
            print(f"     external:     {pkg['external_dependencies']}")
            print(f"     features:     {len(pkg['features'])} items")
            print(f"     key_features: {len(pkg['key_features'])} items")
            print()

        errors = registry.validate(verbose=True)
        if errors:
            print()
            print("Errors:")
            for error in errors:
                print(f"  - {error}")
            sys.exit(1)
        else:
            print()
            print("[OK] All validations passed!")

    except Exception as e:
        print(f"[ERR] Error: {e}")
        import traceback
        traceback.print_exc()
        sys.exit(1)
