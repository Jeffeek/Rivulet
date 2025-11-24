#!/usr/bin/env python3
"""
Sync documentation files from source locations to docs directory.
This script copies README files from packages and converts GitHub-specific
markdown to MkDocs-compatible format.

IMPORTANT: This script reads from packages.yml (single source of truth).
No hardcoding of package paths!
"""
import re
import shutil
import yaml
from pathlib import Path

# Get repository root
# __file__ is docs/readthedocs/sync_docs.py
# DOCS_DIR is docs/readthedocs/
# REPO_ROOT should be the git repository root (two levels up)
DOCS_DIR = Path(__file__).parent
REPO_ROOT = DOCS_DIR.parent.parent

def load_packages_yml():
    """Load packages.yml - the single source of truth for package information."""
    packages_file = REPO_ROOT / "packages.yml"
    with open(packages_file, 'r', encoding='utf-8') as f:
        return yaml.safe_load(f)

def build_sync_files():
    """
    Build SYNC_FILES dictionary dynamically from packages.yml.
    This is the GENERIC solution - no hardcoding!
    """
    sync_files = {}

    # Root documentation files (always included)
    sync_files[REPO_ROOT / "README.md"] = (DOCS_DIR / "index.md", True)  # Convert for MkDocs
    sync_files[REPO_ROOT / "LICENSE.txt"] = (DOCS_DIR / "license.md", False)
    sync_files[REPO_ROOT / "CONTRIBUTING.md"] = (DOCS_DIR / "CONTRIBUTING.md", False)
    sync_files[REPO_ROOT / "SECURITY.md"] = (DOCS_DIR / "SECURITY.md", False)
    sync_files[REPO_ROOT / "CODE_OF_CONDUCT.md"] = (DOCS_DIR / "CODE_OF_CONDUCT.md", False)
    sync_files[REPO_ROOT / "ROADMAP.md"] = (DOCS_DIR / "ROADMAP.md", False)

    # Additional documentation
    sync_files[REPO_ROOT / "tests/Rivulet.Benchmarks/README.md"] = (DOCS_DIR / "benchmarks.md", False)

    # Load packages from packages.yml
    packages_data = load_packages_yml()

    # Add package READMEs dynamically
    for package in packages_data.get('packages', []):
        package_path = package.get('path')
        package_name = package.get('name')

        if not package_path or not package_name:
            continue

        # Source: src/Rivulet.Example/README.md
        source = REPO_ROOT / package_path / "README.md"

        # Destination: docs/readthedocs/packages/rivulet-example.md
        # Convert "Rivulet.Example" -> "rivulet-example"
        dest_name = package_name.lower().replace('.', '-') + '.md'
        destination = DOCS_DIR / "packages" / dest_name

        sync_files[source] = (destination, False)  # Don't convert, copy as-is

    return sync_files

# Build SYNC_FILES dynamically from packages.yml
SYNC_FILES = build_sync_files()


def filter_badges_for_versioned_docs(content: str) -> str:
    """
    GENERIC badge filtering for versioned documentation.

    For versioned docs (v1.3.0, v1.4.0, etc.), badges should be static snapshots.
    This function AUTOMATICALLY detects and removes dynamic badges based on URL patterns,
    WITHOUT hardcoding specific badge services.

    Keep (static badges):
    - opensource.org (license info - static)
    - dotnet.microsoft.com (framework info - static)
    - nuget.org (package badges - acceptable, version-specific)

    Remove (dynamic badges showing current master state):
    - github.com/OWNER/REPO (CI status, release badges for master branch)
    - codecov.io (current coverage, not for frozen version)
    - scorecard.dev (current security score)
    - readthedocs build status (current build, not for frozen version)
    - shields.io badges pointing to github/codecov/etc
    """

    # Pattern to match badge links: <a href="URL"><img src="..." alt="..."></a>
    # We'll examine the href URL to decide if it's dynamic or static

    def is_dynamic_badge(url: str) -> bool:
        """
        Determine if a badge URL is dynamic (shows current state) vs static (frozen info).
        Returns True if badge should be REMOVED from versioned docs.
        """
        url_lower = url.lower()

        # Keep static badges (return False)
        static_domains = [
            'opensource.org',      # License info
            'dotnet.microsoft.com', # .NET framework info
            'nuget.org',           # NuGet package pages
        ]

        for domain in static_domains:
            if domain in url_lower:
                return False  # Keep this badge

        # Remove dynamic badges (return True)
        dynamic_patterns = [
            'github.com/',         # GitHub repo badges (CI, release, etc.)
            'codecov.io/',         # Coverage badges
            'scorecard.dev/',      # Security score badges
            # Note: readthedocs badges are usually shields.io with readthedocs in the URL
            # They'll be caught by the shields.io check below
        ]

        for pattern in dynamic_patterns:
            if pattern in url_lower:
                return True  # Remove this badge

        # Special case: shields.io badges
        # Keep shields.io for static info (nuget, .NET version)
        # Remove shields.io for dynamic info (readthedocs build, github, codecov)
        if 'shields.io' in url_lower or 'img.shields.io' in url_lower:
            # Check if it's pointing to dynamic data
            dynamic_shields_patterns = [
                'readthedocs',    # Build status
                'github',         # GitHub repo data
                'codecov',        # Coverage data
            ]
            for pattern in dynamic_shields_patterns:
                if pattern in url_lower:
                    return True  # Remove dynamic shields.io badge

            # Otherwise keep it (likely NuGet or .NET version badge)
            return False

        # Default: keep badges we don't recognize
        return False

    def filter_badge(match):
        """Filter individual badge based on URL."""
        full_match = match.group(0)
        href_url = match.group(1)

        # Also check the img src URL (important for shields.io badges)
        img_match = re.search(r'<img[^>]+src="([^"]+)"', full_match)
        img_src = img_match.group(1) if img_match else ''

        # Remove badge if EITHER the href or img src is dynamic
        if is_dynamic_badge(href_url) or is_dynamic_badge(img_src):
            # Remove this badge (return empty string)
            return ''
        else:
            # Keep this badge (return original)
            return full_match

    # Match badge pattern: <a href="URL">...<img...>...</a>
    # Using non-greedy matching to avoid matching across multiple badges
    content = re.sub(
        r'<a href="([^"]+)">[^<]*<img[^>]*>[^<]*</a>\s*',
        filter_badge,
        content
    )

    # Also remove CI/CD Pipeline markdown badges (if any)
    content = re.sub(
        r'!\[CI/CD Pipeline\].*?\n',
        '',
        content,
        flags=re.MULTILINE
    )

    return content


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

    # Convert HTML href links to package documentation
    # Pattern: <a href="src/Rivulet.Core/README.md">Docs</a> -> <a href="packages/rivulet-core/">Docs</a>
    content = re.sub(
        r'<a href="src/Rivulet\.([^/]+)/README\.md">',
        lambda m: f'<a href="packages/rivulet-{m.group(1).lower().replace(".", "-")}/">',
        content
    )

    # Fix other relative links to package READMEs
    content = re.sub(
        r'\(src/([^)]+)/README\.md\)',
        lambda m: f'(packages/{m.group(1).split("/")[-1].lower().replace(".", "-")}.md)',
        content
    )

    # GENERIC badge filtering for versioned docs
    # Instead of hardcoding specific badges, we filter by URL patterns
    content = filter_badges_for_versioned_docs(content)

    # Improve packages table rendering for ReadTheDocs
    # Convert stacked badges in table cells to horizontal layout
    def improve_table_cell(match):
        cell_content = match.group(1)

        # If cell contains multiple lines with badges, make them horizontal
        lines = [line.strip() for line in cell_content.split('<br/>') if line.strip()]
        if len(lines) > 1:
            # Separate NuGet badges from Docs link
            badges = [line for line in lines if '<img' in line]
            docs_link = [line for line in lines if 'Docs</a>' in line]

            if badges and docs_link:
                # Put badges horizontally, then docs link below with spacing
                horizontal_badges = ' '.join(badges)
                return f'{horizontal_badges}<br/><br/>{docs_link[0]}'

        return cell_content

    # Process table cells containing badges
    content = re.sub(
        r'<td>\s*((?:<a[^>]*>.*?</a>\s*(?:<br/>)?)+)\s*</td>',
        lambda m: f'<td>\n{improve_table_cell(m)}\n</td>',
        content,
        flags=re.DOTALL
    )

    # Make package names in table smaller (reduce font size)
    # Pattern: <td><strong>emoji PackageName</strong></td>
    content = re.sub(
        r'<td><strong>(.*?Rivulet\.[^<]+)</strong></td>',
        lambda m: f'<td><span style="font-size: 0.9em; font-weight: 600;">{m.group(1)}</span></td>',
        content
    )

    # Generic markdown link conversion - automatically converts ALL .md links
    content = convert_markdown_links(content)

    return content


def convert_markdown_links(content: str) -> str:
    """
    GENERIC solution for VERSIONED DOCS: Automatically convert ALL markdown links to either:
    1. Relative docs paths (if file is synced to docs via SYNC_FILES) ✅
    2. Remove the link/line (if file exists but not synced) ❌ No GitHub URLs for versioned docs!
    3. Keep as-is (external URLs like nuget.org, anchors)

    Why no GitHub URLs?
    - ReadTheDocs docs are versioned (v1.3.0, v1.4.0, etc.)
    - Each version should be static/frozen in time
    - GitHub URLs point to master branch which keeps changing
    - This would break the "snapshot" nature of versioned documentation

    This works for ANY .md file without hardcoding specific filenames.
    No cherry-picking required - fully automatic!
    """

    def convert_link(match):
        link_text = match.group(1)
        link_path = match.group(2)

        # Skip external URLs (already correct)
        if link_path.startswith(('http://', 'https://')):
            return match.group(0)

        # Skip anchors (already correct)
        if link_path.startswith('#'):
            return match.group(0)

        # Special case: LICENSE (without extension) → LICENSE.txt
        # Many people link to LICENSE without .txt extension
        if link_path == 'LICENSE':
            link_path = 'LICENSE.txt'

        # Resolve the linked file (assume relative to REPO_ROOT since README is at root)
        linked_file = REPO_ROOT / link_path

        # Try to normalize the path
        try:
            linked_file = linked_file.resolve()
        except Exception:
            # If path resolution fails, keep original link
            return match.group(0)

        # Check if this file is in SYNC_FILES (will be synced to docs)
        if linked_file in SYNC_FILES:
            # File is synced! Convert to docs path
            dest_path, _ = SYNC_FILES[linked_file]
            rel_path = dest_path.relative_to(DOCS_DIR)
            return f'[{link_text}]({rel_path})'

        # File exists in repo but NOT synced to docs
        # For versioned docs, we CANNOT use GitHub URLs (they point to master)
        # Solution: Remove the link/line entirely
        if linked_file.exists():
            # Check if this is a list item or a sentence
            start_pos = match.start()
            line_start = content.rfind('\n', 0, start_pos) + 1
            line_prefix = content[line_start:start_pos]

            if line_prefix.strip().startswith('-'):
                # List item - mark entire line for removal
                return '___REMOVE_THIS_LINE___'
            else:
                # Inline link - mark just the link for removal
                return '___REMOVE_THIS_LINK___'

        # File doesn't exist - remove the link line if it's in a list
        start_pos = match.start()
        line_start = content.rfind('\n', 0, start_pos) + 1
        line_prefix = content[line_start:start_pos]

        if line_prefix.strip().startswith('-'):
            # This is a list item with a broken link - mark for removal
            return '___REMOVE_THIS_LINE___'

        # Keep original link as fallback (shouldn't happen often)
        return match.group(0)

    # Match [text](path) but NOT ![text](path) (images)
    # Negative lookbehind (?<!\!) ensures we don't match image links
    content = re.sub(
        r'(?<!\!)\[([^\]]+)\]\(([^\)]+)\)',
        convert_link,
        content
    )

    # Remove lines that contain broken links (marked with our special marker)
    content = re.sub(
        r'^.*___REMOVE_THIS_LINE___.*\n',
        '',
        content,
        flags=re.MULTILINE
    )

    # Remove inline links that are marked for removal (keep the surrounding text)
    content = re.sub(
        r'___REMOVE_THIS_LINK___',
        '',
        content
    )

    return content

def sync_docs():
    """Copy and convert documentation files from source locations."""
    print("Syncing documentation files...")

    # Copy assets directory to static/images
    assets_source = REPO_ROOT / "assets"
    static_images_dest = DOCS_DIR / "assets"
    if assets_source.exists():
        # Remove existing static/images directory if it exists
        if static_images_dest.exists():
            shutil.rmtree(static_images_dest)

        # Create parent directory
        static_images_dest.parent.mkdir(parents=True, exist_ok=True)

        # Copy all assets to static/images (no filtering)
        shutil.copytree(assets_source, static_images_dest)
        print(f"  [OK] assets/ directory copied to assets/")

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
