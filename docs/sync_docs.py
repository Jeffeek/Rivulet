#!/usr/bin/env python3
"""
Sync documentation files from source locations to docs directory.
This script copies README files from packages and converts GitHub-specific
markdown to MkDocs-compatible format.
"""
import re
import shutil
from pathlib import Path

# Get repository root (parent of docs directory)
DOCS_DIR = Path(__file__).parent
REPO_ROOT = DOCS_DIR.parent

# Define source -> destination mappings
SYNC_FILES = {
    # Root documentation files (will be converted for MkDocs)
    REPO_ROOT / "README.md": (DOCS_DIR / "index.md", True),  # Convert for MkDocs
    REPO_ROOT / "LICENSE.txt": (DOCS_DIR / "license.md", False),
    REPO_ROOT / "CONTRIBUTING.md": (DOCS_DIR / "CONTRIBUTING.md", False),
    REPO_ROOT / "SECURITY.md": (DOCS_DIR / "SECURITY.md", False),
    REPO_ROOT / "CODE_OF_CONDUCT.md": (DOCS_DIR / "CODE_OF_CONDUCT.md", False),

    # packages (copy as-is)
    REPO_ROOT / "src/Rivulet.Core/README.md": (DOCS_DIR / "packages/rivulet-core.md", False),
    REPO_ROOT / "src/Rivulet.Diagnostics/README.md": (DOCS_DIR / "packages/rivulet-diagnostics.md", False),
    REPO_ROOT / "src/Rivulet.Diagnostics.OpenTelemetry/README.md": (DOCS_DIR / "packages/rivulet-diagnostics-opentelemetry.md", False),
    REPO_ROOT / "src/Rivulet.Testing/README.md": (DOCS_DIR / "packages/rivulet-testing.md", False),
    REPO_ROOT / "src/Rivulet.Hosting/README.md": (DOCS_DIR / "packages/rivulet-hosting.md", False),
    REPO_ROOT / "src/Rivulet.Http/README.md": (DOCS_DIR / "packages/rivulet-http.md", False),
    REPO_ROOT / "src/Rivulet.Sql/README.md": (DOCS_DIR / "packages/rivulet-sql.md", False),
    REPO_ROOT / "src/Rivulet.Sql.SqlServer/README.md": (DOCS_DIR / "packages/rivulet-sql-sqlserver.md", False),
    REPO_ROOT / "src/Rivulet.Sql.PostgreSql/README.md": (DOCS_DIR / "packages/rivulet-sql-postgresql.md", False),
    REPO_ROOT / "src/Rivulet.Sql.MySql/README.md": (DOCS_DIR / "packages/rivulet-sql-mysql.md", False),
    REPO_ROOT / "src/Rivulet.Polly/README.md": (DOCS_DIR / "packages/rivulet-polly.md", False),
}


def convert_github_to_mkdocs(content: str) -> str:
    """
    Convert GitHub-specific markdown to MkDocs-compatible format.

    Conversions:
    - Extract badges from <div align="center"> blocks
    - Fix image paths (assets/logo.png -> ../assets/logo.png)
    - Convert package links from src/*/README.md to packages/*.md
    - Remove GitHub-specific HTML tags
    """

    # Fix image paths: assets/ -> assets/ (assets folder is copied to docs/assets/)
    # No need to change the path since assets will be in docs/assets/
    # content = content.replace('src="assets/', 'src="assets/')  # No-op, keeping same path

    # Process <div align="center"> blocks - extract content and check if it contains badges or images
    def process_center_div(match):
        div_content = match.group(1).strip()

        # Check if this is a logo image (single <img> tag)
        if div_content.startswith('<img') and div_content.count('<img') == 1:
            # Keep image with center alignment
            return f'\n<div align="center">\n{div_content}\n</div>\n'

        # Check if this is a badge block (contains badge markdown)
        if '![' in div_content:
            # Extract badge lines (filter out empty lines and CI/CD badges already removed)
            badge_lines = [line.strip() for line in div_content.split('\n')
                          if line.strip() and '![' in line]
            if badge_lines:
                # Convert badge markdown to HTML directly (don't rely on markdown processor)
                # Pattern: [![alt](image-url)](link-url) -> <a href="link-url"><img src="image-url" alt="alt"></a>
                html_badges = []
                for badge in badge_lines:
                    # Match [![alt](img-url)](link-url) pattern
                    match = re.match(r'\[\!\[([^\]]*)\]\(([^\)]+)\)\]\(([^\)]+)\)', badge)
                    if match:
                        alt_text, img_url, link_url = match.groups()
                        html_badges.append(f'<a href="{link_url}"><img src="{img_url}" alt="{alt_text}"></a>')

                if html_badges:
                    badges_html = ' '.join(html_badges)
                    return f'\n<p align="center">\n{badges_html}\n</p>\n'

                # Fallback: if regex didn't match, use markdown approach
                badges = ' '.join(badge_lines)
                return f'\n<div class="badges" markdown="1" align="center">\n\n{badges}\n\n</div>\n'

        # Default: keep content without div wrapper
        return f'\n{div_content}\n'

    # Match <div align="center"> blocks with their content
    content = re.sub(
        r'<div align="center">\s*\n(.*?)\n\s*</div>',
        process_center_div,
        content,
        flags=re.DOTALL
    )

    # Convert package documentation links from src/*/README.md to packages/*.md
    # Pattern: [Docs](src/Rivulet.Core/README.md) -> [Docs](packages/rivulet-core.md)
    content = re.sub(
        r'\[Docs\]\(src/Rivulet\.([^/]+)/README\.md\)',
        lambda m: f'[Docs](packages/rivulet-{m.group(1).lower().replace(".", "-")}.md)',
        content
    )

    # Fix other relative links to package READMEs
    content = re.sub(
        r'\(src/([^)]+)/README\.md\)',
        lambda m: f'(packages/{m.group(1).split("/")[-1].lower().replace(".", "-")}.md)',
        content
    )

    # Remove CI/CD Pipeline badges section (GitHub Actions specific)
    content = re.sub(
        r'!\[CI/CD Pipeline\].*?\n',
        '',
        content,
        flags=re.MULTILINE
    )

    return content

def sync_docs():
    """Copy and convert documentation files from source locations."""
    print("Syncing documentation files...")

    # Copy assets directory
    assets_source = REPO_ROOT / "assets"
    assets_dest = DOCS_DIR / "assets"
    if assets_source.exists():
        # Remove existing assets directory if it exists
        if assets_dest.exists():
            shutil.rmtree(assets_dest)
        # Copy entire assets directory
        shutil.copytree(assets_source, assets_dest)
        print(f"  [OK] assets/ directory copied")

    for source, (dest, convert) in SYNC_FILES.items():
        if not source.exists():
            print(f"  WARNING: Source not found: {source}")
            continue

        # Ensure destination directory exists
        dest.parent.mkdir(parents=True, exist_ok=True)

        if convert:
            # Read, convert, and write
            content = source.read_text(encoding='utf-8')
            converted_content = convert_github_to_mkdocs(content)
            dest.write_text(converted_content, encoding='utf-8')
            print(f"  [OK] {source.name} -> {dest.relative_to(DOCS_DIR)} (converted)")
        else:
            # Copy as-is
            shutil.copy2(source, dest)
            print(f"  [OK] {source.name} -> {dest.relative_to(DOCS_DIR)}")

    print(f"[OK] Synced {len(SYNC_FILES)} documentation files")

if __name__ == "__main__":
    sync_docs()
