"""Assemble one source-pinned, locally unpublished MAP-014 Windows review ZIP.

Run only after the production publish has passed actual packaged app tests.
No game, network, or project mutation beyond one new ignored release artifact.
"""
import argparse
import hashlib
import json
import re
import subprocess
import zipfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / 'artifacts' / 'map014-takeover' / 'publish'
TOP_FILES = frozenset({
    'e_sqlite3.dll', 'LWBridge.Desktop.deps.json', 'LWBridge.Desktop.dll',
    'LWBridge.Desktop.exe', 'LWBridge.Desktop.runtimeconfig.json',
    'LWBridge.Map_0_3_17.dll', 'Microsoft.Data.Sqlite.dll',
    'Microsoft.Web.WebView2.Core.dll', 'Microsoft.Web.WebView2.Core.xml',
    'Microsoft.Web.WebView2.WinForms.dll', 'Microsoft.Web.WebView2.WinForms.xml',
    'Microsoft.Windows.SDK.NET.dll', 'SQLitePCLRaw.batteries_v2.dll',
    'SQLitePCLRaw.core.dll', 'SQLitePCLRaw.provider.e_sqlite3.dll',
    'WebView2Loader.dll', 'WinRT.Runtime.dll',
})
TOOLS_FILES = frozenset({
    'LiveResourceProbe/current_client_compat.py',
    'LiveResourceProbe/current_live_resource_probe.lua',
    'LiveResourceProbe/run_live_resource_probe.py',
    'LiveResourceProbe/run_live_resource_probe_current.py',
    'OverviewBridge/current_client_compat.py',
    'OverviewBridge/current_live_resource_probe.lua',
    'OverviewBridge/current_overview_bridge.lua',
    'OverviewBridge/LWBridge.GamePipeAdapter.dll',
    'OverviewBridge/recover_overview_pending_current.py',
    'OverviewBridge/run_live_resource_probe.py',
    'OverviewBridge/run_overview_bridge.py',
    'OverviewBridge/run_overview_bridge_current.py',
    'runtimes/win-x64/native/e_sqlite3.dll',
    'runtimes/win-x64/native/WebView2Loader.dll',
})
UI_FILES = frozenset({
    'ProductionUi/index.html', 'ProductionUi/lwbridge-ui-build.json',
    'ProductionUi/.vite/manifest.json',
    'WebUi/index.html', 'WebUi/presentation.css', 'WebUi/preview-host.js',
})
UI_ASSET = re.compile(r'^(?:ProductionUi|WebUi)/assets/[A-Za-z0-9_.-]+-[A-Za-z0-9_-]{8,}\.(?:js|css|png)$')

# dotnet publish copies native SQLite/WebView libraries for every platform,
# while this review candidate is Windows x64 only. Explicitly enumerate the
# known off-platform files: an arbitrary new file below runtimes/ must fail
# the private-file admission rather than disappearing into a broad directory
# exclusion. Both required Windows x64 native libraries remain allowlisted.
NON_TARGET_RUNTIME_FILES = frozenset({
    'runtimes/browser-wasm/nativeassets/net9.0/e_sqlite3.a',
    *(f'runtimes/{runtime}/native/libe_sqlite3.so' for runtime in (
        'linux-arm', 'linux-arm64', 'linux-armel', 'linux-mips64',
        'linux-musl-arm', 'linux-musl-arm64', 'linux-musl-riscv64',
        'linux-musl-s390x', 'linux-musl-x64', 'linux-ppc64le',
        'linux-riscv64', 'linux-s390x', 'linux-x64', 'linux-x86',
    )),
    *(f'runtimes/{runtime}/native/libe_sqlite3.dylib' for runtime in (
        'maccatalyst-arm64', 'maccatalyst-x64', 'osx-arm64', 'osx-x64',
    )),
    'runtimes/win-arm64/native/e_sqlite3.dll',
    'runtimes/win-arm64/native/WebView2Loader.dll',
    'runtimes/win-x86/native/e_sqlite3.dll',
    'runtimes/win-x86/native/WebView2Loader.dll',
})


def sha(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def permitted(relative: Path) -> bool:
    name = relative.as_posix()
    return (name in TOP_FILES or name in TOOLS_FILES or name in UI_FILES
            or UI_ASSET.fullmatch(name) is not None)


def excluded_non_target_runtime(relative: Path) -> bool:
    return relative.as_posix() in NON_TARGET_RUNTIME_FILES


def create() -> dict:
    sha_commit = subprocess.run(['git', 'rev-parse', 'HEAD'], cwd=ROOT,
                                capture_output=True, text=True, check=True).stdout.strip()
    if len(sha_commit) != 40 or not all(char in '0123456789abcdef' for char in sha_commit):
        raise RuntimeError('Git source identity unavailable')
    changed = subprocess.run(['git', 'status', '--porcelain=v1', '--untracked-files=normal'],
                             cwd=ROOT, capture_output=True, text=True, check=True).stdout.strip()
    if changed:
        raise RuntimeError('Cannot source-pin release: tracked or untracked repository changes remain')
    if not (SOURCE / 'LWBridge.Desktop.exe').is_file():
        raise FileNotFoundError('The tested production executable is absent')
    escaped_exe = str(SOURCE / 'LWBridge.Desktop.exe').replace("'", "''")
    version = subprocess.run(['powershell', '-NoProfile', '-Command',
                              f"(Get-Item -LiteralPath '{escaped_exe}').VersionInfo.ProductVersion"],
                             capture_output=True, text=True, check=True).stdout.strip()
    if version.rsplit('+', 1)[-1] != sha_commit:
        raise RuntimeError(f'Published EXE source identity {version!r} does not match clean HEAD')
    subprocess.run(['node', str(ROOT / 'src/LWBridge.UI-0.3.17/scripts/check-production-build.mjs'),
                    str(SOURCE / 'ProductionUi')], cwd=ROOT, capture_output=True, text=True, check=True)
    included = []
    exclusions = []
    runtime_exclusions = []
    for source in sorted(SOURCE.rglob('*')):
        if not source.is_file():
            continue
        relative = source.relative_to(SOURCE)
        parts = {part.casefold() for part in relative.parts}
        if relative.suffix.lower() in {'.pdb', '.pyc'} or '__pycache__' in parts:
            exclusions.append(str(relative))
            continue
        if excluded_non_target_runtime(relative):
            runtime_exclusions.append(str(relative))
            continue
        if not permitted(relative):
            raise ValueError(f'Unexpected/unapproved release file: {relative}')
        included.append((source, relative))
    if len(included) < 60 or len(included) > 100:
        raise ValueError(f'Unexpected publish manifest length: {len(included)}')

    short = sha_commit[:12]
    destination = ROOT / 'artifacts' / f'release-map014-{short[:8]}'
    destination.mkdir(parents=True, exist_ok=True)
    package = destination / f'LW-Control-MAP014-OWNER-RC-{short}.zip'
    if package.exists():
        raise FileExistsError(f'The one final candidate already exists: {package}')
    temporary = package.with_suffix('.zip.staging')
    if temporary.exists():
        raise FileExistsError(f'A prior interrupted staging artifact needs review: {temporary}')
    recorded = {}
    try:
        with zipfile.ZipFile(temporary, 'x', compression=zipfile.ZIP_DEFLATED, compresslevel=9) as archive:
            for source, relative in included:
                key = relative.as_posix()
                archive.write(source, key)
                recorded[key] = sha(source)
        with zipfile.ZipFile(temporary, 'r') as archive:
            if archive.testzip() is not None:
                raise RuntimeError('Release ZIP CRC validation failed')
            if set(archive.namelist()) != set(recorded):
                raise RuntimeError('Release ZIP entries do not match published manifest')
            for name, expected in recorded.items():
                if hashlib.sha256(archive.read(name)).hexdigest() != expected:
                    raise RuntimeError(f'Release ZIP byte mismatch: {name}')
        temporary.replace(package)
    finally:
        if temporary.exists():
            temporary.unlink()
    result = {
        'sourceCommit': sha_commit,
        'executableProductVersion': version,
        'source': str(SOURCE),
        'package': str(package),
        'sizeBytes': package.stat().st_size,
        'sha256': sha(package),
        'executableSha256': recorded['LWBridge.Desktop.exe'],
        'files': len(included),
        'excludedDebugArtifacts': exclusions,
        'excludedNonTargetRuntimeAssets': runtime_exclusions,
        'manifest': recorded,
        'crcAndByteIdentityPass': True,
        'privateFileExclusionPass': True,
        'published': False,
    }
    (destination / 'release-integrity.json').write_text(json.dumps(result, indent=2), encoding='utf8')
    return {key: value for key, value in result.items() if key != 'manifest'}


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.parse_args()
    print(json.dumps(create(), indent=2))
