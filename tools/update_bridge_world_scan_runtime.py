"""Safely attach the persistent World Scan runtime to the current LWC2 bridge package."""

from __future__ import annotations

import argparse
from datetime import datetime, timezone
import hashlib
import json
import os
from pathlib import Path
import shutil
import tempfile

try:
    from .extract_lenc_v3 import decode_lenc_bytes, derive_xlua_key_nonce, encode_lenc_bytes
    from .install_loader_probe import (
        InstallRefused,
        LUA_ENTRY,
        ORIGINAL_LUA_ENTRY,
        _entry_map,
        _make_backup,
        _restore_from_backup,
        _xlua_path,
        crc32_file,
        discover_paths,
        game_is_running,
        read_lwlf,
        sha256_file,
        write_lwlf,
    )
    from .prepare_daily_task_runtime import WORLD_SCAN_ENTRY
except ImportError:
    from extract_lenc_v3 import decode_lenc_bytes, derive_xlua_key_nonce, encode_lenc_bytes
    from install_loader_probe import (
        InstallRefused,
        LUA_ENTRY,
        ORIGINAL_LUA_ENTRY,
        _entry_map,
        _make_backup,
        _restore_from_backup,
        _xlua_path,
        crc32_file,
        discover_paths,
        game_is_running,
        read_lwlf,
        sha256_file,
        write_lwlf,
    )
    from prepare_daily_task_runtime import WORLD_SCAN_ENTRY


BRIDGE_MARKER = "-- LWC2_ENTRY: clean LastWarControl bridge wrapper."
WORLD_SCAN_MARKER = "-- LWC2_WORLD_SCAN_RUNTIME: persistent v8 attachment."
MANIFEST_NAME = "bridge-world-scan-runtime-update.json"
EXPECTED_FILE_VERSION = 3
EXPECTED_CONTENT_VERSION = 12
EXPECTED_WORLD_SCAN_VERSION = 'local M = { VERSION = "lwcontrol-world-full-scan-probe-9" }'
EXPECTED_CLEAN_BASEUTILS_SHA256 = "b20865f42c9272e0fca2b6deb2e9142576b12dcf42f11ad1a8816ddd59b952d6"
EXPECTED_ORIGINAL_LUA_ENTRY_SHA256 = "50f3ae906a8e9898549c4ea740eedc772a88eb2979e165eb35733192d100a137"


def _world_scan_source() -> bytes:
    return Path(__file__).with_name("current_world_map_full_scan_probe.lua").read_bytes()


def _hashes(paths: dict[str, Path]) -> dict[str, str]:
    return {key: sha256_file(paths[key]) for key in ("data", "metadata", "version", "baseutils")}


def _manifest_path(paths: dict[str, Path]) -> Path:
    return paths["runtime"] / MANIFEST_NAME


def _decode_world_scan(mapped: dict[str, bytes], paths: dict[str, Path]) -> bytes:
    encoded = mapped.get(WORLD_SCAN_ENTRY)
    if encoded is None:
        raise InstallRefused(f"{WORLD_SCAN_ENTRY} is missing from the current package")
    native = derive_xlua_key_nonce(_xlua_path(paths))
    return decode_lenc_bytes(encoded, native["key"], native["nonce"])["decoded"]


def _decode_if_lenc(data: bytes, paths: dict[str, Path]) -> bytes:
    if not data.startswith(b"LENC"):
        return data
    native = derive_xlua_key_nonce(_xlua_path(paths))
    return decode_lenc_bytes(data, native["key"], native["nonce"])["decoded"]


def _find_clean_baseutils(paths: dict[str, Path]) -> Path:
    candidates: list[Path] = []
    for candidate in paths["backup_root"].rglob("BaseUtils.rdl"):
        try:
            if candidate.is_file() and sha256_file(candidate) == EXPECTED_CLEAN_BASEUTILS_SHA256:
                candidates.append(candidate)
        except OSError:
            continue
    if not candidates:
        raise InstallRefused(
            "no recorded clean BaseUtils.rdl backup matches the proven content-v12 IsDebug=false hash"
        )
    return max(candidates, key=lambda item: item.stat().st_mtime_ns)


def _validate_bridge_wrapper(wrapper: str) -> None:
    required = (
        BRIDGE_MARKER,
        'pcall(require, "DataCenter.Global.LuaEntry_original")',
        'pcall(require, "LWC2Bridge")',
        "pcall(bridge.Register)",
        "pcall(bridge.Pump)",
        'rawset(_G, "LWC2Bridge", bridge)',
        "return original",
    )
    missing = [value for value in required if value not in wrapper]
    if missing:
        raise InstallRefused(f"current LuaEntry is not the expected LWC2 bridge wrapper; missing {missing!r}")
    if wrapper.count("pcall(bridge.Pump)") != 1:
        raise InstallRefused("current LWC2 wrapper Pump shape is ambiguous")
    if wrapper.count('rawset(_G, "LWC2Bridge", bridge)') != 1:
        raise InstallRefused("current LWC2 wrapper bridge export shape is ambiguous")


def _desired_wrapper(current: bytes) -> bytes:
    try:
        wrapper = current.decode("utf-8")
    except UnicodeDecodeError as exc:
        raise InstallRefused("current LWC2 LuaEntry is not UTF-8 plaintext") from exc
    _validate_bridge_wrapper(wrapper)
    if WORLD_SCAN_MARKER in wrapper:
        required = (
            'rawset(_G, "LWControlWorldScanPersistentRuntime", true)',
            'pcall(require, "LWControlWorldScanRuntime")',
            "pcall(world_scan.Register)",
            "pcall(world_scan.Pump)",
            'rawset(_G, "LWControlWorldScanRuntime", world_scan)',
        )
        if any(value not in wrapper for value in required):
            raise InstallRefused("existing LWC2 World Scan attachment is incomplete")
        return current

    load_anchor = "if not ok_bridge then error(bridge) end\n"
    register_anchor = 'mark("registered", tostring(ok_register) .. ":" .. tostring(register_result))\n'
    export_anchor = 'rawset(_G, "LWC2Bridge", bridge)\n'
    for anchor in (load_anchor, register_anchor, export_anchor):
        if wrapper.count(anchor) != 1:
            raise InstallRefused(f"current LWC2 wrapper anchor is ambiguous: {anchor.strip()}")

    load_block = (
        load_anchor
        + "\n"
        + WORLD_SCAN_MARKER
        + "\n"
        + 'rawset(_G, "LWControlWorldScanPersistentRuntime", true)\n'
        + 'local ok_world_scan, world_scan = pcall(require, "LWControlWorldScanRuntime")\n'
        + 'mark("world_scan_loaded", ok_world_scan and "ok" or tostring(world_scan))\n'
        + "if not ok_world_scan then error(world_scan) end\n"
    )
    register_block = (
        register_anchor
        + "local ok_world_register, world_register_result = pcall(world_scan.Register)\n"
        + 'mark("world_scan_registered", tostring(ok_world_register) .. ":" .. tostring(world_register_result))\n'
    )
    export_block = (
        'rawset(_G, "LWControlWorldScanRuntime", world_scan)\n'
        + export_anchor
    )
    wrapper = wrapper.replace(load_anchor, load_block, 1)
    wrapper = wrapper.replace(register_anchor, register_block, 1)
    wrapper = wrapper.replace("        pcall(bridge.Pump)\n", "        pcall(bridge.Pump)\n        pcall(world_scan.Pump)\n", 1)
    wrapper = wrapper.replace(export_anchor, export_block, 1)
    return wrapper.encode("utf-8")


def _atomic_copy(source: Path, destination: Path) -> None:
    descriptor, name = tempfile.mkstemp(prefix=f".{destination.name}.world-scan-", suffix=".tmp", dir=destination.parent)
    os.close(descriptor)
    temporary = Path(name)
    try:
        shutil.copy2(source, temporary)
        os.replace(temporary, destination)
    finally:
        temporary.unlink(missing_ok=True)


def inspect(paths: dict[str, Path]) -> dict[str, object]:
    file_version, content_version, entries = read_lwlf(paths["data"])
    mapped = _entry_map(entries)
    active = mapped.get(LUA_ENTRY)
    original = mapped.get(ORIGINAL_LUA_ENTRY)
    if active is None or original is None:
        raise InstallRefused("current package is missing active/preserved LuaEntry")
    try:
        wrapper = _decode_if_lenc(active, paths).decode("utf-8")
        _validate_bridge_wrapper(wrapper)
    except (UnicodeDecodeError, InstallRefused) as exc:
        return {
            "file_version": file_version,
            "content_version": content_version,
            "entry_count": len(entries),
            "bridge_wrapper_valid": False,
            "error": str(exc),
            "installed": False,
            "hashes": _hashes(paths),
        }
    decoded = _decode_world_scan(mapped, paths)
    source = _world_scan_source()
    attached = WORLD_SCAN_MARKER in wrapper
    plaintext_entries = sorted(name for name, data in entries if not data.startswith(b"LENC"))
    unexpected_plaintext = [
        name for name in plaintext_entries
        if name != LUA_ENTRY and not (name.startswith("LWC2") and name.endswith(".luac"))
    ]
    original_hash = hashlib.sha256(original).hexdigest()
    clean_baseutils = sha256_file(paths["baseutils"]) == EXPECTED_CLEAN_BASEUTILS_SHA256
    return {
        "file_version": file_version,
        "content_version": content_version,
        "entry_count": len(entries),
        "bridge_wrapper_valid": True,
        "world_scan_attached": attached,
        "world_scan_source_matches": decoded == source,
        "world_scan_version_v9": EXPECTED_WORLD_SCAN_VERSION.encode("utf-8") in decoded,
        "world_scan_decoded_sha256": hashlib.sha256(decoded).hexdigest(),
        "active_wrapper_lenc": active.startswith(b"LENC"),
        "plaintext_entry_count": len(plaintext_entries),
        "unexpected_plaintext_entries": unexpected_plaintext,
        "original_lua_entry_sha256": original_hash,
        "original_lua_entry_verified": original_hash == EXPECTED_ORIGINAL_LUA_ENTRY_SHA256,
        "clean_baseutils": clean_baseutils,
        "installed": attached
        and active.startswith(b"LENC")
        and not plaintext_entries
        and decoded == source
        and EXPECTED_WORLD_SCAN_VERSION.encode("utf-8") in decoded
        and original_hash == EXPECTED_ORIGINAL_LUA_ENTRY_SHA256
        and clean_baseutils,
        "hashes": _hashes(paths),
        "manifest": str(_manifest_path(paths)) if _manifest_path(paths).is_file() else None,
    }


def install(paths: dict[str, Path]) -> dict[str, object]:
    if game_is_running():
        raise InstallRefused("LastWar.exe is running; close it before updating the bridge World Scan runtime")
    file_version, content_version, entries = read_lwlf(paths["data"])
    if file_version != EXPECTED_FILE_VERSION or content_version != EXPECTED_CONTENT_VERSION:
        raise InstallRefused(
            f"current package version {(file_version, content_version)!r} is outside the proven v3/content-v12 contract"
        )
    mapped = _entry_map(entries)
    active = mapped.get(LUA_ENTRY)
    original = mapped.get(ORIGINAL_LUA_ENTRY)
    if active is None or original is None:
        raise InstallRefused("current package is missing active/preserved LuaEntry")
    if hashlib.sha256(original).hexdigest() != EXPECTED_ORIGINAL_LUA_ENTRY_SHA256:
        raise InstallRefused("preserved official LuaEntry no longer matches the proven content-v12 entry")
    desired_wrapper = _desired_wrapper(_decode_if_lenc(active, paths))
    source = _world_scan_source()
    if EXPECTED_WORLD_SCAN_VERSION.encode("utf-8") not in source:
        raise InstallRefused("current World Scan source is not probe v9")
    native = derive_xlua_key_nonce(_xlua_path(paths))
    encoded_wrapper = encode_lenc_bytes(desired_wrapper, native["key"], native["nonce"])
    encoded_world_scan = encode_lenc_bytes(source, native["key"], native["nonce"])
    if decode_lenc_bytes(encoded_wrapper, native["key"], native["nonce"])["decoded"] != desired_wrapper:
        raise InstallRefused("LWC2 wrapper LENC round-trip failed")
    if decode_lenc_bytes(encoded_world_scan, native["key"], native["nonce"])["decoded"] != source:
        raise InstallRefused("probe v9 LENC round-trip failed")

    changed_names = {LUA_ENTRY, WORLD_SCAN_ENTRY}
    converted_entries: list[str] = []
    replaced: list[tuple[str, bytes]] = []
    for name, data in entries:
        if name == LUA_ENTRY:
            replaced.append((name, encoded_wrapper))
        elif name == WORLD_SCAN_ENTRY:
            replaced.append((name, encoded_world_scan))
        elif not data.startswith(b"LENC"):
            if not (name.startswith("LWC2") and name.endswith(".luac")):
                raise InstallRefused(f"unexpected plaintext package entry {name}; refusing broad conversion")
            encoded = encode_lenc_bytes(data, native["key"], native["nonce"])
            if decode_lenc_bytes(encoded, native["key"], native["nonce"])["decoded"] != data:
                raise InstallRefused(f"LENC round-trip failed for custom entry {name}")
            replaced.append((name, encoded))
            changed_names.add(name)
            converted_entries.append(name)
        else:
            replaced.append((name, data))
    if WORLD_SCAN_ENTRY not in mapped:
        raise InstallRefused(f"{WORLD_SCAN_ENTRY} is missing; refusing to append a new runtime entry")

    paths["runtime"].mkdir(parents=True, exist_ok=True)
    candidate_dir = Path(tempfile.mkdtemp(prefix="bridge-world-scan-v9-", dir=paths["runtime"]))
    candidate_data = candidate_dir / "LWScripts.data"
    candidate_metadata = candidate_dir / "LWScripts.txt"
    write_lwlf(candidate_data, file_version, content_version, replaced)
    candidate_metadata.write_text(f"{candidate_data.stat().st_size}|{crc32_file(candidate_data)}", encoding="utf-8")

    verify_file_version, verify_content_version, verify_entries = read_lwlf(candidate_data)
    verify_map = _entry_map(verify_entries)
    if (verify_file_version, verify_content_version) != (file_version, content_version):
        raise InstallRefused("candidate package header changed unexpectedly")
    if decode_lenc_bytes(verify_map[LUA_ENTRY], native["key"], native["nonce"])["decoded"] != desired_wrapper:
        raise InstallRefused("candidate LWC2 wrapper did not round-trip through LENC")
    if decode_lenc_bytes(verify_map[WORLD_SCAN_ENTRY], native["key"], native["nonce"])["decoded"] != source:
        raise InstallRefused("candidate World Scan v9 payload did not round-trip")
    not_lenc = [name for name, data in verify_entries if not data.startswith(b"LENC")]
    if not_lenc:
        raise InstallRefused(f"candidate still contains plaintext .luac payloads: {not_lenc[:5]!r}")
    for name, before in entries:
        if name not in changed_names and verify_map.get(name) != before:
            raise InstallRefused(f"candidate changed unrelated package entry {name}")

    before_hashes = _hashes(paths)
    clean_baseutils = _find_clean_baseutils(paths)
    backup = _make_backup(paths)
    manifest_path = _manifest_path(paths)
    try:
        _atomic_copy(candidate_data, paths["data"])
        _atomic_copy(candidate_metadata, paths["metadata"])
        _atomic_copy(clean_baseutils, paths["baseutils"])
        after_hashes = _hashes(paths)
        if after_hashes["version"] != before_hashes["version"]:
            raise InstallRefused("version.txt changed during World Scan runtime update")
        if after_hashes["baseutils"] != EXPECTED_CLEAN_BASEUTILS_SHA256:
            raise InstallRefused("BaseUtils.rdl did not restore to the proven IsDebug=false hash")
        final_file_version, final_content_version, final_entries = read_lwlf(paths["data"])
        final_map = _entry_map(final_entries)
        if (final_file_version, final_content_version) != (file_version, content_version):
            raise InstallRefused("installed package header does not match candidate")
        if decode_lenc_bytes(final_map[LUA_ENTRY], native["key"], native["nonce"])["decoded"] != desired_wrapper:
            raise InstallRefused("installed LWC2 wrapper does not match verified encrypted candidate")
        if decode_lenc_bytes(final_map[WORLD_SCAN_ENTRY], native["key"], native["nonce"])["decoded"] != source:
            raise InstallRefused("installed World Scan runtime does not match probe v9 source")
        final_plaintext = [name for name, data in final_entries if not data.startswith(b"LENC")]
        if final_plaintext:
            raise InstallRefused(f"installed package still contains plaintext entries: {final_plaintext[:5]!r}")
        for name, before in entries:
            if name not in changed_names and final_map.get(name) != before:
                raise InstallRefused(f"installed package changed unrelated entry {name}")
        expected_metadata = f"{paths['data'].stat().st_size}|{crc32_file(paths['data'])}"
        if paths["metadata"].read_text(encoding="utf-8").strip() != expected_metadata:
            raise InstallRefused("installed LWScripts.txt does not match package size/CRC")
        manifest = {
            "schemaVersion": 1,
            "updatedAt": datetime.now(timezone.utc).isoformat().replace("+00:00", "Z"),
            "worldScanVersion": "lwcontrol-world-full-scan-probe-9",
            "backupDirectory": str(backup),
            "changedEntries": sorted(changed_names),
            "convertedPlaintextEntries": sorted(converted_entries),
            "cleanBaseUtilsSource": str(clean_baseutils),
            "beforeHashes": before_hashes,
            "afterHashes": after_hashes,
            "wrapperSha256": hashlib.sha256(desired_wrapper).hexdigest(),
            "worldScanSourceSha256": hashlib.sha256(source).hexdigest(),
            "baseutilsRestoredClean": after_hashes["baseutils"] == EXPECTED_CLEAN_BASEUTILS_SHA256,
            "versionPreserved": after_hashes["version"] == before_hashes["version"],
        }
        temporary_manifest = manifest_path.with_name(f".{manifest_path.name}.{os.getpid()}.tmp")
        temporary_manifest.write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
        os.replace(temporary_manifest, manifest_path)
        return {
            "changed": True,
            "backup": str(backup),
            "candidate_dir": str(candidate_dir),
            "manifest": str(manifest_path),
            "before_hashes": before_hashes,
            "after_hashes": after_hashes,
            "inspection": inspect(paths),
        }
    except Exception:
        _restore_from_backup(paths, backup)
        manifest_path.unlink(missing_ok=True)
        raise


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    group = parser.add_mutually_exclusive_group(required=True)
    group.add_argument("--status", action="store_true")
    group.add_argument("--install", action="store_true")
    parser.add_argument("--json", action="store_true")
    args = parser.parse_args()
    try:
        paths = discover_paths()
        result = install(paths) if args.install else inspect(paths)
    except (InstallRefused, OSError, ValueError, KeyError) as exc:
        payload = {"ok": False, "error": str(exc), "error_type": type(exc).__name__}
        print(json.dumps(payload, indent=2) if args.json else f"REFUSED: {exc}")
        return 2
    payload = {"ok": True, **result}
    print(json.dumps(payload, indent=2) if args.json else payload)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
