#!/usr/bin/env python3
"""
Sync documentation files from source locations to docs directory.
This script copies README files from packages to docs/packages/ before building.
"""
import shutil
from pathlib import Path

# Get repository root (parent of docs directory)
DOCS_DIR = Path(__file__).parent
REPO_ROOT = DOCS_DIR.parent

# Define source -> destination mappings
SYNC_FILES = {
    # Root documentation files
    REPO_ROOT / "README.md": DOCS_DIR / "readme-main.md",
    REPO_ROOT / "LICENSE.txt": DOCS_DIR / "license.md",
    REPO_ROOT / "CONTRIBUTING.md": DOCS_DIR / "CONTRIBUTING.md",
    REPO_ROOT / "SECURITY.md": DOCS_DIR / "SECURITY.md",
    REPO_ROOT / "CODE_OF_CONDUCT.md": DOCS_DIR / "CODE_OF_CONDUCT.md",

    # packages
    REPO_ROOT / "src/Rivulet.Core/README.md": DOCS_DIR / "packages/rivulet-core.md",
    REPO_ROOT / "src/Rivulet.Diagnostics/README.md": DOCS_DIR / "packages/rivulet-diagnostics.md",
    REPO_ROOT / "src/Rivulet.Diagnostics.OpenTelemetry/README.md": DOCS_DIR / "packages/rivulet-diagnostics-opentelemetry.md",
    REPO_ROOT / "src/Rivulet.Testing/README.md": DOCS_DIR / "packages/rivulet-testing.md",
    REPO_ROOT / "src/Rivulet.Hosting/README.md": DOCS_DIR / "packages/rivulet-hosting.md",
    REPO_ROOT / "src/Rivulet.Http/README.md": DOCS_DIR / "packages/rivulet-http.md",
    REPO_ROOT / "src/Rivulet.Sql/README.md": DOCS_DIR / "packages/rivulet-sql.md",
    REPO_ROOT / "src/Rivulet.Sql.SqlServer/README.md": DOCS_DIR / "packages/rivulet-sql-sqlserver.md",
    REPO_ROOT / "src/Rivulet.Sql.PostgreSql/README.md": DOCS_DIR / "packages/rivulet-sql-postgresql.md",
    REPO_ROOT / "src/Rivulet.Sql.MySql/README.md": DOCS_DIR / "packages/rivulet-sql-mysql.md",
    REPO_ROOT / "src/Rivulet.Polly/README.md": DOCS_DIR / "packages/rivulet-polly.md",
}

def sync_docs():
    """Copy documentation files from source locations."""
    print("Syncing documentation files...")

    for source, dest in SYNC_FILES.items():
        if not source.exists():
            print(f"  WARNING: Source not found: {source}")
            continue

        # Ensure destination directory exists
        dest.parent.mkdir(parents=True, exist_ok=True)

        # Copy file
        shutil.copy2(source, dest)
        print(f"  [OK] {source.name} -> {dest.relative_to(DOCS_DIR)}")

    print(f"[OK] Synced {len(SYNC_FILES)} documentation files")

if __name__ == "__main__":
    sync_docs()
