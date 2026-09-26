#!/usr/bin/env python3
"""Extract and verify Last War locale cache assets from the owner's reference LWBridge executable."""

from __future__ import annotations

import argparse
import gzip
import hashlib
import json
import os
from pathlib import Path
import re
import sys
import uuid

EXPECTED_REFERENCE_SHA256 = "2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff"
EXPECTED_MANIFEST_VERSION = 1
EXPECTED_MANIFEST_FORMAT = "json-gzip"
EXPECTED_SOURCE_VERSION = "23443"


def sha256(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def balanced_json(blob: bytes, start: int) -> bytes | None:
    depth = 0
    quoted = False
    escaped = False
    for pos in range(start, min(len(blob), start + 65536)):
        ch = blob[pos]
        if quoted:
            if escaped:
                escaped = False
            elif ch == 0x5C:
                escaped = True
            elif ch == 0x22:
                quoted = False
            continue
        if ch == 0x22:
            quoted = True
        elif ch == 0x7B:
            depth += 1
        elif ch == 0x7D:
            depth -= 1
            if depth == 0:
                return blob[start : pos + 1]
    return None

def find_manifest(blob: bytes) -> tuple[dict, bytes]:
    needle = b'"languages": {'
    needle_pos = blob.find(needle)
    if needle_pos < 0:
        raise RuntimeError("embedded locale manifest marker not found")

    search_start = max(0, needle_pos - 4096)
    start = needle_pos
    while True:
        start = blob.rfind(b"{", search_start, start)
        if start < 0:
            break
        raw = balanced_json(blob, start)
        if raw is not None:
            try:
                parsed = json.loads(raw)
            except (UnicodeDecodeError, json.JSONDecodeError):
                parsed = None
            if isinstance(parsed, dict):
                if (parsed.get("version") == EXPECTED_MANIFEST_VERSION
                        and parsed.get("format") == EXPECTED_MANIFEST_FORMAT
                        and parsed.get("sourceVersion") == EXPECTED_SOURCE_VERSION
                        and isinstance(parsed.get("languages"), dict)
                        and "en" in parsed["languages"]):
                    return parsed, raw
    raise RuntimeError("verified embedded locale manifest not found")

def validate_manifest(manifest: dict) -> dict[str, dict]:
    languages = manifest.get("languages")
    if not isinstance(languages, dict) or not languages:
        raise RuntimeError("locale manifest languages must be a non-empty object")
    validated: dict[str, dict] = {}
    for language, entry in languages.items():
        if not isinstance(language, str) or not language or not isinstance(entry, dict):
            raise RuntimeError("invalid locale manifest language entry")
        file_name = entry.get("file")
        size = entry.get("size")
        digest = entry.get("sha256")
        if not isinstance(file_name, str) or Path(file_name).name != file_name:
            raise RuntimeError(f"invalid locale filename for {language}")
        if not isinstance(size, int) or size <= 0:
            raise RuntimeError(f"invalid locale size for {language}")
        if not isinstance(digest, str) or len(digest) != 64:
            raise RuntimeError(f"invalid locale sha256 for {language}")
        try:
            bytes.fromhex(digest)
        except ValueError as exc:
            raise RuntimeError(f"invalid locale sha256 for {language}") from exc
        validated[language] = {**entry, "sha256": digest.lower()}
    return validated

def find_asset(blob: bytes, file_name: str, size: int, digest: str) -> bytes:
    encoded_name = file_name.encode("utf-8")
    position = 0
    while True:
        name_pos = blob.find(encoded_name, position)
        if name_pos < 0:
            break
        window_start = max(0, name_pos - 64)
        window_end = min(len(blob) - size, name_pos + len(encoded_name) + 256)
        gzip_pos = window_start
        while gzip_pos <= window_end:
            gzip_pos = blob.find(b"\x1f\x8b\x08", gzip_pos, window_end + 1)
            if gzip_pos < 0:
                break
            candidate = blob[gzip_pos : gzip_pos + size]
            if len(candidate) == size and sha256(candidate) == digest:
                return candidate
            gzip_pos += 1
        position = name_pos + 1
    raise RuntimeError(f"hash-matching embedded locale blob not found: {file_name}")


def verify_payload(language: str, compressed: bytes) -> int:
    try:
        payload = json.loads(gzip.decompress(compressed))
    except (OSError, UnicodeDecodeError, json.JSONDecodeError) as exc:
        raise RuntimeError(f"invalid embedded locale payload: {language}") from exc
    if not isinstance(payload, dict):
        raise RuntimeError(f"embedded locale payload is not an object: {language}")
    if any(not isinstance(key, str) or not isinstance(value, str) for key, value in payload.items()):
        raise RuntimeError(f"embedded locale payload has non-string entries: {language}")
    return len(payload)

def publish(destination: Path, manifest_raw: bytes, assets: dict[str, tuple[str, bytes]]) -> None:
    destination.mkdir(parents=True, exist_ok=True)
    token = uuid.uuid4().hex
    staged: list[tuple[Path, Path]] = []
    for _, (file_name, data) in assets.items():
        temporary = destination / f".{file_name}.{token}.tmp"
        temporary.write_bytes(data)
        staged.append((temporary, destination / file_name))

    manifest_temp = destination / f".manifest.json.{token}.tmp"
    manifest_temp.write_bytes(manifest_raw)
    try:
        for temporary, final in staged:
            os.replace(temporary, final)
        os.replace(manifest_temp, destination / "manifest.json")
    finally:
        for temporary, _ in staged:
            temporary.unlink(missing_ok=True)
        manifest_temp.unlink(missing_ok=True)


def default_destination() -> Path:
    local_app_data = os.environ.get("LOCALAPPDATA")
    if not local_app_data:
        raise RuntimeError("LOCALAPPDATA is unavailable; pass --destination explicitly")
    return Path(local_app_data) / "LWBridgeRebuild" / "locales"


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("reference_exe", type=Path, help="Owner's original lwbridge-0.3.1.exe")
    parser.add_argument("--destination", type=Path, help="Locale cache directory")
    return parser.parse_args()

def main() -> int:
    args = parse_args()
    reference = args.reference_exe.resolve()
    destination = (args.destination or default_destination()).resolve()
    blob = reference.read_bytes()
    actual_reference_hash = sha256(blob)
    if actual_reference_hash != EXPECTED_REFERENCE_SHA256:
        raise RuntimeError(
            "reference executable SHA-256 mismatch: "
            f"{actual_reference_hash} != {EXPECTED_REFERENCE_SHA256}")

    manifest, manifest_raw = find_manifest(blob)
    languages = validate_manifest(manifest)
    assets: dict[str, tuple[str, bytes]] = {}
    counts: dict[str, int] = {}
    for language, entry in languages.items():
        compressed = find_asset(blob, entry["file"], entry["size"], entry["sha256"])
        counts[language] = verify_payload(language, compressed)
        assets[language] = (entry["file"], compressed)

    publish(destination, manifest_raw, assets)
    print(f"reference_sha256={actual_reference_hash}")
    print(f"source_version={manifest.get('sourceVersion')}")
    print(f"destination={destination}")
    for language in languages:
        entry = languages[language]
        print(
            f"{language}: file={entry['file']} size={entry['size']} "
            f"sha256={entry['sha256']} keys={counts[language]}")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (OSError, RuntimeError, ValueError, KeyError) as exc:
        print(f"error: {exc}", file=sys.stderr)
        raise SystemExit(1)
