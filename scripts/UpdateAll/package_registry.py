#!/usr/bin/env python3
"""
Rivulet Package Registry - Core module for loading and validating packages.yml

This module provides the foundation for all package management scripts.
"""

import os
import sys
from pathlib import Path
from typing import Dict, List, Optional, Any
import yaml


class PackageRegistry:
    """
    Loads and provides access to the package registry (packages.yml).
    """

    def __init__(self, registry_path: Optional[Path] = None):
        """
        Initialize the package registry.

        Args:
            registry_path: Path to packages.yml. If None, searches from current directory up to repo root.
        """
        if registry_path is None:
            registry_path = self._find_registry()

        self.registry_path = registry_path
        self.repo_root = registry_path.parent

        with open(registry_path, 'r', encoding='utf-8') as f:
            self.data = yaml.safe_load(f)

        self.packages = self.data.get('packages', [])
        self.metadata = self.data.get('metadata', {})
        self.categories = self.data.get('categories', {})
        self.versions = self.data.get('versions', [])
        self.badges = self.data.get('badges', {})
        self.links = self.data.get('links', {})

        # Create lookup maps
        self._package_by_id = {pkg['id']: pkg for pkg in self.packages}
        self._package_by_name = {pkg['name']: pkg for pkg in self.packages}

    def _find_registry(self) -> Path:
        """Find packages.yml by searching from current directory to repo root."""
        current = Path.cwd()

        for _ in range(10):  # Max 10 levels up
            candidate = current / 'packages.yml'
            if candidate.exists():
                return candidate

            # Check if we're at repo root (has .git directory)
            if (current / '.git').exists():
                candidate = current / 'packages.yml'
                if candidate.exists():
                    return candidate
                else:
                    raise FileNotFoundError(
                        f"packages.yml not found in repository root: {current}"
                    )

            parent = current.parent
            if parent == current:  # Reached filesystem root
                break
            current = parent

        raise FileNotFoundError(
            "packages.yml not found. Make sure you're in the Rivulet repository."
        )

    def get_package(self, identifier: str) -> Optional[Dict[str, Any]]:
        """
        Get package by ID or name.

        Args:
            identifier: Package ID (e.g., 'core') or name (e.g., 'Rivulet.Core')

        Returns:
            Package dictionary or None if not found
        """
        return self._package_by_id.get(identifier) or self._package_by_name.get(identifier)

    def get_packages_by_category(self, category: str) -> List[Dict[str, Any]]:
        """Get all packages in a specific category."""
        return [pkg for pkg in self.packages if pkg.get('category') == category]

    def get_packages_by_status(self, status: str) -> List[Dict[str, Any]]:
        """Get all packages with a specific status."""
        return [pkg for pkg in self.packages if pkg.get('status') == status]

    def get_packages_by_version(self, version: str) -> List[Dict[str, Any]]:
        """Get all packages released in a specific version."""
        version_data = next((v for v in self.versions if v['version'] == version), None)
        if not version_data:
            return []

        package_ids = version_data.get('packages', [])
        return [self._package_by_id[pid] for pid in package_ids if pid in self._package_by_id]

    def get_core_packages(self) -> List[Dict[str, Any]]:
        """Get all core packages."""
        return self.get_packages_by_category('core')

    def get_integration_packages(self) -> List[Dict[str, Any]]:
        """Get all integration packages."""
        return self.get_packages_by_category('integration')

    def get_released_packages(self) -> List[Dict[str, Any]]:
        """Get all released packages."""
        return self.get_packages_by_status('released')

    def get_in_development_packages(self) -> List[Dict[str, Any]]:
        """Get all packages currently in development."""
        return self.get_packages_by_status('in_development')

    def get_nuget_badge_url(self, package: Dict[str, Any]) -> str:
        """Get NuGet version badge URL for a package."""
        return self.badges['nuget_badge'].format(nuget_id=package['nuget_id'])

    def get_nuget_url(self, package: Dict[str, Any]) -> str:
        """Get NuGet package URL for a package."""
        return self.badges['nuget_url'].format(nuget_id=package['nuget_id'])

    def get_nuget_downloads_badge_url(self, package: Dict[str, Any]) -> str:
        """Get NuGet downloads badge URL for a package."""
        return self.badges['nuget_downloads'].format(nuget_id=package['nuget_id'])

    def validate(self, verbose: bool = False) -> List[str]:
        """
        Validate the package registry.

        Args:
            verbose: Print validation progress

        Returns:
            List of error messages (empty if valid)
        """
        errors = []

        if verbose:
            print("Validating package registry...")

        # Check for duplicate IDs
        ids = [pkg['id'] for pkg in self.packages]
        duplicates = [id for id in ids if ids.count(id) > 1]
        if duplicates:
            errors.append(f"Duplicate package IDs: {set(duplicates)}")

        # Check for duplicate names
        names = [pkg['name'] for pkg in self.packages]
        duplicates = [name for name in names if names.count(name) > 1]
        if duplicates:
            errors.append(f"Duplicate package names: {set(duplicates)}")

        # Validate each package
        for pkg in self.packages:
            pkg_errors = self._validate_package(pkg, verbose)
            errors.extend(pkg_errors)

        if verbose:
            if errors:
                print(f"❌ Validation failed with {len(errors)} errors")
            else:
                print("✅ Validation passed!")

        return errors

    def _validate_package(self, pkg: Dict[str, Any], verbose: bool) -> List[str]:
        """Validate a single package."""
        errors = []
        pkg_name = pkg.get('name', 'Unknown')

        if verbose:
            print(f"  Validating {pkg_name}...")

        # Check required fields
        required_fields = ['name', 'id', 'category', 'version', 'status', 'path', 'test_path']
        for field in required_fields:
            if field not in pkg:
                errors.append(f"{pkg_name}: Missing required field '{field}'")

        # Check paths exist
        if 'path' in pkg:
            path = self.repo_root / pkg['path']
            if not path.exists():
                errors.append(f"{pkg_name}: Package path does not exist: {pkg['path']}")
            else:
                # Check for .csproj file
                csproj = path / f"{pkg['name']}.csproj"
                if not csproj.exists():
                    errors.append(f"{pkg_name}: .csproj not found: {csproj}")

        if 'test_path' in pkg:
            test_path = self.repo_root / pkg['test_path']
            if not test_path.exists():
                errors.append(f"{pkg_name}: Test path does not exist: {pkg['test_path']}")

        if 'sample_path' in pkg:
            sample_path = self.repo_root / pkg['sample_path']
            if not sample_path.exists():
                errors.append(f"{pkg_name}: Sample path does not exist: {pkg['sample_path']}")

        # Validate dependencies
        if 'dependencies' in pkg:
            for dep in pkg['dependencies']:
                # dep is a package name like "Rivulet.Core"
                if not self.get_package(dep):
                    errors.append(f"{pkg_name}: Unknown dependency: {dep}")

        # Validate category
        if 'category' in pkg:
            if pkg['category'] not in self.categories:
                errors.append(f"{pkg_name}: Unknown category: {pkg['category']}")

        return errors


def load_registry(registry_path: Optional[Path] = None) -> PackageRegistry:
    """
    Convenience function to load the package registry.

    Args:
        registry_path: Path to packages.yml. If None, auto-discovers.

    Returns:
        PackageRegistry instance
    """
    return PackageRegistry(registry_path)


if __name__ == '__main__':
    # Fix Windows console encoding for emoji support (only in main script)
    if sys.platform == 'win32':
        try:
            import io
            if hasattr(sys.stdout, 'buffer'):
                sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')
            if hasattr(sys.stderr, 'buffer'):
                sys.stderr = io.TextIOWrapper(sys.stderr.buffer, encoding='utf-8')
        except (AttributeError, ValueError):
            pass  # Already wrapped or not needed

    # Test loading and validation
    try:
        registry = load_registry()
        print(f"✅ Loaded {len(registry.packages)} packages from {registry.registry_path}")
        print(f"   Repository root: {registry.repo_root}")
        print()

        errors = registry.validate(verbose=True)
        if errors:
            print()
            print("Errors:")
            for error in errors:
                print(f"  ❌ {error}")
            sys.exit(1)
        else:
            print()
            print("✅ All validations passed!")

    except Exception as e:
        print(f"❌ Error: {e}")
        sys.exit(1)
