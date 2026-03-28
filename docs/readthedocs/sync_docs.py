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
from urllib.parse import urlsplit, urlunsplit
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
    Returns dict: source_path -> (dest_path, convert_bool)
    """
    sync_files = {}

    # Root documentation files (always included, all converted for MkDocs)
    sync_files[REPO_ROOT / "README.md"] = (DOCS_DIR / "index.md", True)
    sync_files[REPO_ROOT / "LICENSE.txt"] = (DOCS_DIR / "license.md", True)
    sync_files[REPO_ROOT / "CONTRIBUTING.md"] = (DOCS_DIR / "CONTRIBUTING.md", True)
    sync_files[REPO_ROOT / "SECURITY.md"] = (DOCS_DIR / "SECURITY.md", True)
    sync_files[REPO_ROOT / "CODE_OF_CONDUCT.md"] = (DOCS_DIR / "CODE_OF_CONDUCT.md", True)
    sync_files[REPO_ROOT / "ROADMAP.md"] = (DOCS_DIR / "ROADMAP.md", True)
    sync_files[REPO_ROOT / "README-RESHARPER.md"] = (DOCS_DIR / "README-RESHARPER.md", True)

    # Additional documentation
    sync_files[REPO_ROOT / "tests/Rivulet.Benchmarks/README.md"] = (DOCS_DIR / "benchmarks.md", True)

    # Load packages from packages.yml
    packages_data = load_packages_yml()

    # Add package READMEs dynamically
    for package in packages_data.get('packages', []):
        package_name = package.get('name')
        if not package_name:
            continue

        # Derive path from name: Rivulet.Example -> src/Rivulet.Example
        source = REPO_ROOT / "src" / package_name / "README.md"

        # Destination: docs/readthedocs/packages/rivulet-example.md
        # Convert "Rivulet.Example" -> "rivulet-example"
        dest_name = package_name.lower().replace('.', '-') + '.md'
        destination = DOCS_DIR / "packages" / dest_name

        sync_files[source] = (destination, True)

    return sync_files

# Build SYNC_FILES dynamically from packages.yml
SYNC_FILES = build_sync_files()


def filter_badges_for_versioned_docs(content: str) -> str:
    """
    Filter badges for versioned documentation.

    Keep static badges (license, .NET version, NuGet).
    Remove dynamic badges (CI status, coverage, security score).
    """

    def is_dynamic_badge(url: str) -> bool:
        """Returns True if badge should be REMOVED from versioned docs."""
        url_lower = url.lower()

        # Keep static badges
        static_domains = [
            'opensource.org',
            'dotnet.microsoft.com',
            'nuget.org',
        ]
        for domain in static_domains:
            if domain in url_lower:
                return False

        # Remove dynamic badges
        dynamic_patterns = [
            'github.com/',
            'codecov.io/',
            'scorecard.dev/',
        ]
        for pattern in dynamic_patterns:
            if pattern in url_lower:
                return True

        # shields.io: keep static (NuGet, .NET), remove dynamic (GitHub, codecov, RTD)
        if 'shields.io' in url_lower or 'img.shields.io' in url_lower:
            dynamic_shields_patterns = ['readthedocs', 'github', 'codecov']
            for pattern in dynamic_shields_patterns:
                if pattern in url_lower:
                    return True
            return False

        # Default: keep badges we don't recognize
        return False

    def filter_badge(match):
        full_match = match.group(0)
        href_url = match.group(1)

        img_match = re.search(r'<img[^>]+src="([^"]+)"', full_match)
        img_src = img_match.group(1) if img_match else ''

        if is_dynamic_badge(href_url) or is_dynamic_badge(img_src):
            return ''
        return full_match

    # Match badge pattern: <a href="URL">...<img...>...</a>
    content = re.sub(
        r'<a href="([^"]+)">[^<]*<img[^>]*>[^<]*</a>\s*',
        filter_badge,
        content
    )

    # Remove CI/CD Pipeline markdown badges
    content = re.sub(
        r'!\[CI/CD Pipeline\].*?\n',
        '',
        content,
        flags=re.MULTILINE
    )

    return content


def _strip_comment_markers(content: str) -> str:
    """
    Remove HTML comment markers used only by code-generation scripts.

    These markers (e.g. <!-- KEY_FEATURES_START -->) are used by generate-all.py
    to locate sections for auto-update but are meaningless in MkDocs output.
    Keeping them in the generated Markdown breaks list rendering: Python-Markdown
    treats a bare <!-- --> line as an HTML block, which can prevent adjacent list
    items from being parsed as <ul>/<li> elements.
    """
    return re.sub(r'<!--\s*\w+_(?:START|END)\s*-->\n?', '', content)


# Footer headings that are standardized and injected by the sync script.
# Existing occurrences in source files are stripped and replaced.
_FOOTER_HEADINGS = ['## Framework Support', '## Documentation & Source', '## License']


def _strip_package_footer(content: str) -> str:
    """
    Remove the standardized footer zone from converted package content.

    The footer zone starts at the earliest occurrence of any of the canonical
    footer headings, or at the bare '---' + 'Made with' block if no heading is
    found first.
    """
    earliest = len(content)
    for heading in _FOOTER_HEADINGS:
        idx = content.find('\n' + heading + '\n')
        if idx != -1 and idx < earliest:
            earliest = idx
    # Also catch the lone --- + footer line when headings are absent
    m = re.search(r'\n\n---\n\n\*\*Made with', content)
    if m and m.start() < earliest:
        earliest = m.start()
    return content[:earliest].rstrip()


def _build_package_footer(package_name: str) -> str:
    """
    Build the canonical footer appended to every package documentation page.

    The NuGet URL is the only per-package variable; everything else is identical
    across all packages so the footer is easy to keep in sync.
    """
    nuget_url = f"https://www.nuget.org/packages/{package_name}/"
    return (
        "\n\n## Framework Support\n\n"
        "- .NET 8.0\n"
        "- .NET 9.0\n"
        "\n## Documentation & Source\n\n"
        "- **GitHub Repository**: [https://github.com/Jeffeek/Rivulet](https://github.com/Jeffeek/Rivulet)\n"
        "- **Report Issues**: [https://github.com/Jeffeek/Rivulet/issues](https://github.com/Jeffeek/Rivulet/issues)\n"
        "- **License**: MIT\n"
        "\n## License\n\n"
        "MIT License - see [LICENSE](../license.md) for details\n"
        "\n---\n\n"
        f"**Made with ❤️ by Jeffeek** | [NuGet]({nuget_url}) | [GitHub](https://github.com/Jeffeek/Rivulet)\n"
    )


def convert_github_to_mkdocs(content: str, source_path: Path) -> str:
    """
    Convert GitHub-specific markdown to MkDocs-compatible format.

    Args:
        content: The markdown content to convert.
        source_path: Path to the source file (for resolving relative links).
    """
    source_dir = source_path.parent

    # Strip source-only HTML comment markers before any other processing.
    # Must be done first so that comment blocks don't interfere with list parsing.
    content = _strip_comment_markers(content)

    # Process <div align="center"> blocks
    def process_center_div(match):
        div_content = match.group(1).strip()

        # Logo image — keep centered
        if div_content.startswith('<img') and div_content.count('<img') == 1:
            return f'\n<div align="center">\n{div_content}\n</div>\n'

        # Badge block — convert to horizontal HTML badges
        if '![' in div_content:
            badge_lines = [line.strip() for line in div_content.split('\n')
                          if line.strip() and '![' in line]
            if badge_lines:
                html_badges = []
                for badge in badge_lines:
                    m = re.match(r'\[\!\[([^\]]*)\]\(([^\)]+)\)\]\(([^\)]+)\)', badge)
                    if m:
                        alt_text, img_url, link_url = m.groups()
                        html_badges.append(f'<a href="{link_url}"><img src="{img_url}" alt="{alt_text}"></a>')

                if html_badges:
                    badges_html = ' '.join(html_badges)
                    return f'\n<p align="center">\n{badges_html}\n</p>\n'

                badges = ' '.join(badge_lines)
                return f'\n<div class="badges" markdown="1" align="center">\n\n{badges}\n\n</div>\n'

        return f'\n{div_content}\n'

    content = re.sub(
        r'<div align="center">\s*\n(.*?)\n\s*</div>',
        process_center_div,
        content,
        flags=re.DOTALL
    )

    # Convert package documentation links: src/Rivulet.*/README.md -> packages/*.md
    content = re.sub(
        r'\[Docs\]\(src/Rivulet\.([^/]+)/README\.md\)',
        lambda m: f'[Docs](packages/rivulet-{m.group(1).lower().replace(".", "-")}.md)',
        content
    )

    content = re.sub(
        r'<a href="src/Rivulet\.([^/]+)/README\.md">',
        lambda m: f'<a href="packages/rivulet-{m.group(1).lower().replace(".", "-")}/">',
        content
    )

    content = re.sub(
        r'\(src/([^)]+)/README\.md\)',
        lambda m: f'(packages/{m.group(1).split("/")[-1].lower().replace(".", "-")}.md)',
        content
    )

    # Filter dynamic badges for versioned docs
    content = filter_badges_for_versioned_docs(content)

    # Improve packages table rendering — horizontal badges
    def improve_table_cell(match):
        cell_content = match.group(1)
        lines = [line.strip() for line in cell_content.split('<br/>') if line.strip()]
        if len(lines) > 1:
            badges = [line for line in lines if '<img' in line]
            docs_link = [line for line in lines if 'Docs</a>' in line]
            if badges and docs_link:
                horizontal_badges = ' '.join(badges)
                return f'{horizontal_badges}<br/><br/>{docs_link[0]}'
        return cell_content

    content = re.sub(
        r'<td>\s*((?:<a[^>]*>.*?</a>\s*(?:<br/>)?)+)\s*</td>',
        lambda m: f'<td>\n{improve_table_cell(m)}\n</td>',
        content,
        flags=re.DOTALL
    )

    content = re.sub(
        r'<td><strong>(.*?Rivulet\.[^<]+)</strong></td>',
        lambda m: f'<td><span style="font-size: 0.9em; font-weight: 600;">{m.group(1)}</span></td>',
        content
    )

    # Convert markdown links (source-aware)
    content = convert_markdown_links(content, source_dir)

    return content


def convert_markdown_links(content: str, source_dir: Path) -> str:
    """
    Convert relative markdown links to docs paths or remove them.

    For versioned docs, we cannot use GitHub URLs (they point to master).
    Links to synced files are converted; links to unsynced files are removed.

    Args:
        content: The markdown content.
        source_dir: Directory of the source file (for resolving relative links).
    """

    # Pre-resolve SYNC_FILES keys for reliable cross-platform comparison
    resolved_sync = {k.resolve(): v for k, v in SYNC_FILES.items()}

    def convert_link(match):
        link_text = match.group(1)
        link_path = match.group(2)

        # Skip external URLs and anchors
        if link_path.startswith(('http://', 'https://', '#')):
            return match.group(0)

        # Strip fragment and query string before resolving
        parts = urlsplit(link_path)
        link_path = parts.path
        suffix = urlunsplit(('', '', '', parts.query, parts.fragment))  # ?query#fragment

        # Special case: LICENSE (without extension) -> LICENSE.txt
        if link_path == 'LICENSE':
            link_path = 'LICENSE.txt'

        # Resolve the linked file relative to the source file's directory
        linked_file = (source_dir / link_path).resolve()

        # Check if this file is in SYNC_FILES (will be synced to docs)
        if linked_file in resolved_sync:
            dest_path, _ = resolved_sync[linked_file]
            rel_path = dest_path.relative_to(DOCS_DIR)
            return f'[{link_text}]({rel_path.as_posix()}{suffix})'

        # File exists in repo but NOT synced to docs — remove for versioned docs
        if linked_file.exists():
            start_pos = match.start()
            line_start = content.rfind('\n', 0, start_pos) + 1
            line_prefix = content[line_start:start_pos]

            if line_prefix.strip().startswith('-'):
                return '___REMOVE_THIS_LINE___'
            else:
                return '___REMOVE_THIS_LINK___'

        # File doesn't exist — remove if in a list item
        start_pos = match.start()
        line_start = content.rfind('\n', 0, start_pos) + 1
        line_prefix = content[line_start:start_pos]

        if line_prefix.strip().startswith('-'):
            return '___REMOVE_THIS_LINE___'

        # Keep original link as fallback
        return match.group(0)

    # Match [text](path) but NOT ![text](path) (images)
    content = re.sub(
        r'(?<!\!)\[([^\]]+)\]\(([^\)]+)\)',
        convert_link,
        content
    )

    # Remove lines marked for removal
    content = re.sub(
        r'^.*___REMOVE_THIS_LINE___.*\n',
        '',
        content,
        flags=re.MULTILINE
    )

    # Remove inline link markers
    content = re.sub(
        r'___REMOVE_THIS_LINK___',
        '',
        content
    )

    return content

def sync_docs():
    """Copy and convert documentation files from source locations."""
    print("Syncing documentation files...")

    # Copy assets directory
    assets_source = REPO_ROOT / "assets"
    assets_dest = DOCS_DIR / "assets"
    if assets_source.exists():
        if assets_dest.exists():
            shutil.rmtree(assets_dest)
        assets_dest.parent.mkdir(parents=True, exist_ok=True)
        shutil.copytree(assets_source, assets_dest)
        print(f"  [OK] assets/ directory copied to assets/")

    for source, (dest, convert) in SYNC_FILES.items():
        if not source.exists():
            print(f"  WARNING: Source not found: {source}")
            continue

        # Ensure destination directory exists
        dest.parent.mkdir(parents=True, exist_ok=True)

        if convert:
            content = source.read_text(encoding='utf-8')
            converted_content = convert_github_to_mkdocs(content, source)

            # Inject canonical footer for package READMEs.
            # src/<PackageName>/README.md -> strip any existing footer zone and
            # append the standardised Framework Support / Documentation & Source /
            # License sections so every package page is identical in structure.
            if source.parent.parent == REPO_ROOT / 'src' and source.name == 'README.md':
                package_name = source.parent.name
                converted_content = _strip_package_footer(converted_content)
                converted_content += _build_package_footer(package_name)

            # Normalise to LF line endings so the committed files are consistent
            # regardless of the OS the script is run on, and MkDocs/Python-Markdown
            # on the ReadTheDocs Linux server receives clean input.
            converted_content = converted_content.replace('\r\n', '\n')

            with dest.open('w', encoding='utf-8', newline='\n') as f:
                f.write(converted_content)
            print(f"  [OK] {source.name} -> {dest.relative_to(DOCS_DIR)} (converted)")
        else:
            shutil.copy2(source, dest)
            print(f"  [OK] {source.name} -> {dest.relative_to(DOCS_DIR)}")

    print(f"[OK] Synced {len(SYNC_FILES)} documentation files")

if __name__ == "__main__":
    sync_docs()
