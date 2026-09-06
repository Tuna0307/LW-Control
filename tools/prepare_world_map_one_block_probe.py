"""Build or verify the encrypted single-block World AOI proof package.

Preparation is offline-only. It preserves the official LuaEntry in a separate
entry, installs the already-proven loader wrapper, and replaces only
LWControlProbe with the bounded one-block probe source.
"""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path

try:
    from .extract_lenc_v3 import decode_lenc_bytes, derive_xlua_key_nonce, encode_lenc_bytes
    from .install_loader_probe import (
        InstallRefused,
        LUA_ENTRY,
        ORIGINAL_LUA_ENTRY,
        PROBE_ENTRY,
        _entry_map,
        _entry_source,
        _xlua_path,
        crc32_file,
        discover_paths,
        preflight,
        read_lwlf,
        write_lwlf,
    )
except ImportError:
    from extract_lenc_v3 import decode_lenc_bytes, derive_xlua_key_nonce, encode_lenc_bytes
    from install_loader_probe import (
        InstallRefused,
        LUA_ENTRY,
        ORIGINAL_LUA_ENTRY,
        PROBE_ENTRY,
        _entry_map,
        _entry_source,
        _xlua_path,
        crc32_file,
        discover_paths,
        preflight,
        read_lwlf,
        write_lwlf,
    )


def _probe_source() -> bytes:
    return Path(__file__).with_name("current_world_map_one_block_probe.lua").read_bytes()


def _encoded_entries(paths: dict[str, Path], entries: list[tuple[str, bytes]]):
    mapped = _entry_map(entries)
    official = mapped.get(LUA_ENTRY)
    if official is None:
        raise InstallRefused(f"{LUA_ENTRY} is missing from LWScripts.data")
    native = derive_xlua_key_nonce(_xlua_path(paths))
    wrapper_source = _entry_source()
    probe_source = _probe_source()
    wrapper = encode_lenc_bytes(wrapper_source, native["key"], native["nonce"])
    probe = encode_lenc_bytes(probe_source, native["key"], native["nonce"])
    if decode_lenc_bytes(wrapper, native["key"], native["nonce"])["decoded"] != wrapper_source:
        raise InstallRefused("one-block wrapper LENC round-trip failed")
    if decode_lenc_bytes(probe, native["key"], native["nonce"])["decoded"] != probe_source:
        raise InstallRefused("one-block probe LENC round-trip failed")

    mapped[ORIGINAL_LUA_ENTRY] = bytes(official)
    mapped[LUA_ENTRY] = wrapper
    mapped[PROBE_ENTRY] = probe
    output: list[tuple[str, bytes]] = []
    seen: set[str] = set()
    for name, _ in entries:
        output.append((name, mapped[name]))
        seen.add(name)
    for name in (ORIGINAL_LUA_ENTRY, PROBE_ENTRY):
        if name not in seen:
            output.append((name, mapped[name]))
    return output, native, bytes(official), wrapper_source, probe_source


def prepare(paths: dict[str, Path], output_dir: Path) -> dict[str, object]:
    before = preflight(paths)
    if before["file_version"] != 3:
        raise InstallRefused("one-block probe requires current LWLF version 3")
    output_dir = output_dir.resolve()
    output_dir.mkdir(parents=True, exist_ok=True)
    outputs = [output_dir / "LWScripts.data", output_dir / "LWScripts.txt", output_dir / "version.txt"]
    if any(path.exists() for path in outputs):
        raise InstallRefused("one-block candidate output already exists")

    file_version, content_version, entries = read_lwlf(paths["data"])
    prepared, native, official, wrapper_source, probe_source = _encoded_entries(paths, entries)
    write_lwlf(outputs[0], file_version, content_version, prepared)
    verify_version, verify_content, verify_entries = read_lwlf(outputs[0])
    verify_map = _entry_map(verify_entries)
    if (verify_version, verify_content) != (file_version, content_version):
        raise InstallRefused("one-block candidate header did not round-trip")
    if verify_map.get(ORIGINAL_LUA_ENTRY) != official:
        raise InstallRefused("one-block candidate did not preserve official LuaEntry")
    if decode_lenc_bytes(verify_map[LUA_ENTRY], native["key"], native["nonce"])["decoded"] != wrapper_source:
        raise InstallRefused("one-block serialized wrapper verification failed")
    if decode_lenc_bytes(verify_map[PROBE_ENTRY], native["key"], native["nonce"])["decoded"] != probe_source:
        raise InstallRefused("one-block serialized probe verification failed")

    length = outputs[0].stat().st_size
    crc = crc32_file(outputs[0])
    outputs[1].write_text(f"{length}|{crc}", encoding="utf-8")
    outputs[2].write_text(str(content_version), encoding="utf-8")
    return {
        "changed_installed_files": False,
        "output_dir": str(output_dir),
        "file_version": file_version,
        "content_version": content_version,
        "entry_count": len(verify_entries),
        "package_length": length,
        "package_crc32": crc,
        "package_sha256": hashlib.sha256(outputs[0].read_bytes()).hexdigest(),
        "probe_source_sha256": hashlib.sha256(probe_source).hexdigest(),
        "serialized_round_trip_verified": True,
        "preflight": before,
    }


def verify(paths: dict[str, Path], candidate_dir: Path) -> dict[str, object]:
    candidate_dir = candidate_dir.resolve()
    data = candidate_dir / "LWScripts.data"
    metadata = candidate_dir / "LWScripts.txt"
    version = candidate_dir / "version.txt"
    if not data.is_file() or not metadata.is_file() or not version.is_file():
        raise InstallRefused("one-block candidate is incomplete")
    file_version, content_version, entries = read_lwlf(data)
    mapped = _entry_map(entries)
    native = derive_xlua_key_nonce(_xlua_path(paths))
    official_file_version, official_content_version, official_entries = read_lwlf(paths["data"])
    official_map = _entry_map(official_entries)
    if (file_version, content_version) != (official_file_version, official_content_version):
        raise InstallRefused("one-block candidate LWLF version does not match installed build")
    if mapped.get(ORIGINAL_LUA_ENTRY) != official_map.get(LUA_ENTRY):
        raise InstallRefused("one-block candidate original LuaEntry does not match installed build")
    if decode_lenc_bytes(mapped[LUA_ENTRY], native["key"], native["nonce"])["decoded"] != _entry_source():
        raise InstallRefused("one-block candidate wrapper does not match expected source")
    if decode_lenc_bytes(mapped[PROBE_ENTRY], native["key"], native["nonce"])["decoded"] != _probe_source():
        raise InstallRefused("one-block candidate probe does not match expected source")
    expected_length, expected_crc = metadata.read_text(encoding="utf-8").strip().split("|", 1)
    if int(expected_length) != data.stat().st_size or int(expected_crc) != crc32_file(data):
        raise InstallRefused("one-block candidate metadata length/CRC mismatch")
    if int(version.read_text(encoding="utf-8").strip()) != content_version:
        raise InstallRefused("one-block candidate version.txt mismatch")
    return {
        "candidate_dir": str(candidate_dir),
        "file_version": file_version,
        "content_version": content_version,
        "entry_count": len(entries),
        "package_sha256": hashlib.sha256(data.read_bytes()).hexdigest(),
        "probe_source_sha256": hashlib.sha256(_probe_source()).hexdigest(),
        "verified": True,
    }


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    group = parser.add_mutually_exclusive_group(required=True)
    group.add_argument("--prepare-dir", type=Path)
    group.add_argument("--verify-dir", type=Path)
    parser.add_argument("--json", action="store_true")
    args = parser.parse_args()
    try:
        paths = discover_paths()
        result = prepare(paths, args.prepare_dir) if args.prepare_dir else verify(paths, args.verify_dir)
    except (InstallRefused, OSError, ValueError, KeyError) as exc:
        payload = {"ok": False, "error": str(exc), "error_type": type(exc).__name__}
        print(json.dumps(payload, indent=2) if args.json else f"REFUSED: {exc}")
        return 2
    payload = {"ok": True, **result}
    print(json.dumps(payload, indent=2) if args.json else payload)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
