#!/usr/bin/env python3
"""Read-only Last War Lua update diagnostics; never repairs or changes game files.

File CRCs are ordinary zlib CRC-32 over current file bytes. Log values are
observations, not invented decoder semantics. A successful report is not proof
the launcher or game can start. Only an explicitly requested output is written.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import os
import re
import zlib
from datetime import datetime, timezone
from pathlib import Path


def fingerprint(path: Path) -> dict:
    if not path.is_file():
        return {"exists": False}
    before = path.stat()
    digest, crc = hashlib.sha256(), 0
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
            crc = zlib.crc32(chunk, crc)
    after = path.stat()
    return {
        "exists": True, "size": after.st_size, "sha256": digest.hexdigest(),
        "rawCrc32": crc,
        "modifiedAtUtc": datetime.fromtimestamp(after.st_mtime, timezone.utc).isoformat(),
        "stableDuringRead": (before.st_size, before.st_mtime_ns) == (after.st_size, after.st_mtime_ns),
    }


def inspect(root: Path, scripts: Path) -> dict:
    log = root / "Launcher.log"
    lines = log.read_text(encoding="utf-8", errors="replace").splitlines() if log.is_file() else []
    patch_pattern = re.compile(r'Downloading LWLuaFile Patch \{ remote_file_name: "(LWScripts_[A-Za-z0-9_.-]+\.patch)", save_file_name: "([A-Za-z0-9_.-]+)", size: (\d+), crc: (\d+) \}')
    error_pattern = re.compile(r'LWLua decode failed: crc mismatch for .*LWScripts\.data\.tmp.*expected (\d+), got (\d+)')
    patch, failure, patch_at_failure = None, None, None
    for number, line in enumerate(lines, 1):
        if match := patch_pattern.search(line):
            patch = {"line": number, "name": match[1], "savedName": match[2], "advertisedSize": int(match[3]), "advertisedCrc32": int(match[4])}
        if match := error_pattern.search(line):
            failure = {"line": number, "timestampAsLogged": line.split(']')[0].lstrip('['), "target": "lwScripts/LWScripts.data.tmp", "expectedCrc32": int(match[1]), "reportedCrc32": int(match[2])}
            patch_at_failure = patch
    files = {name: fingerprint(scripts/name) for name in ('LWScripts.data', 'LWScripts.data.tmp', 'LWScripts.txt', 'version.txt')}
    metadata_path, version_path = scripts/'LWScripts.txt', scripts/'version.txt'
    metadata = metadata_path.read_text(encoding='utf-8-sig').strip() if metadata_path.is_file() else ''
    version = version_path.read_text(encoding='utf-8-sig').strip() if version_path.is_file() else ''
    pair = re.fullmatch(r'(\d+)\|(\d+)', metadata)
    current = files['LWScripts.data']
    pending = files['LWScripts.data.tmp']
    checks = {}
    if pair and current.get('exists'):
        checks['activeSizeMatchesMetadata'] = current['size'] == int(pair[1])
        checks['activeRawCrcMatchesMetadata'] = current['rawCrc32'] == int(pair[2])
    if patch_at_failure:
        name = patch_at_failure['savedName']
        files[name] = fingerprint(scripts/name)
        patch_file = files[name]
        if patch_file.get('exists'):
            checks['patchSizeMatchesAdvertisement'] = patch_file['size'] == patch_at_failure['advertisedSize']
            checks['patchRawCrcMatchesAdvertisement'] = patch_file['rawCrc32'] == patch_at_failure['advertisedCrc32']
    if failure and pending.get('exists'):
        checks['pendingRawCrcMatchesReportedFailure'] = pending['rawCrc32'] == failure['reportedCrc32']
        checks['pendingRawCrcMatchesExpectedOutput'] = pending['rawCrc32'] == failure['expectedCrc32']
    return {
        'schemaVersion': 1, 'observedAtUtc': datetime.now(timezone.utc).isoformat(),
        'mode': 'read-only', 'scriptsRoot': '%USERPROFILE%/AppData/LocalLow/FunFly/Last War-Survival Game/lwScripts',
        'sourceLog': {'name': 'Launcher.log', **fingerprint(log)},
        'latestRecordedCrcFailure': failure, 'precedingPatchAdvertisement': patch_at_failure,
        'files': files, 'activeMetadata': metadata if pair else None,
        'activeVersionMarker': version if re.fullmatch(r'\d+', version) else None,
        'checks': checks,
        'limits': ['Historical log events do not alone establish current launcher health.',
                   'Matching downloaded patch CRC does not prove the publisher patch/decoder is correct.',
                   'No patch application, decryption, game launch, repair or installed-file mutation performed.',
                   'Inspect stableDuringRead and recheck after any update/repair before relying on hashes.'],
    }


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--root', type=Path, default=Path(os.environ.get('LOCALAPPDATA', ''))/'FunFly'/'Last War-Survival Game')
    parser.add_argument('--scripts-root', type=Path, default=Path(os.environ.get('USERPROFILE', ''))/'AppData'/'LocalLow'/'FunFly'/'Last War-Survival Game'/'lwScripts')
    parser.add_argument('--output', type=Path)
    args = parser.parse_args()
    report = inspect(args.root, args.scripts_root)
    serialized = json.dumps(report, indent=2)+'\n'
    if args.output:
        output = args.output.resolve()
        for protected in (args.root.resolve(), args.scripts_root.resolve()):
            if output == protected or protected in output.parents:
                parser.error('output must be outside the installed game and script directories')
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(serialized, encoding='utf-8')
    print(serialized, end='')
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
