"""Build or verify the encrypted bounded full-scan World AOI proof package.

Preparation is offline-only. It preserves the official LuaEntry in a separate
entry, installs the already-proven loader wrapper, and replaces only
LWControlProbe with the bounded full-scan probe source.
"""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
import time

try:
    from .extract_lenc_v3 import decode_lenc_bytes, derive_xlua_key_nonce, encode_lenc_bytes
    from .install_loader_probe import (
        InstallRefused,
        EXPECTED_LUA_ENTRY_SHA256,
        LUA_ENTRY,
        ORIGINAL_LUA_ENTRY,
        _entry_map,
        _xlua_path,
        crc32_file,
        discover_paths,
        preflight,
        read_lwlf,
        write_lwlf,
    )
    from .install_daily_task_runtime import inspect as inspect_daily_task_runtime
except ImportError:
    from extract_lenc_v3 import decode_lenc_bytes, derive_xlua_key_nonce, encode_lenc_bytes
    from install_loader_probe import (
        InstallRefused,
        EXPECTED_LUA_ENTRY_SHA256,
        LUA_ENTRY,
        ORIGINAL_LUA_ENTRY,
        _entry_map,
        _xlua_path,
        crc32_file,
        discover_paths,
        preflight,
        read_lwlf,
        write_lwlf,
    )
    from install_daily_task_runtime import inspect as inspect_daily_task_runtime


def _probe_source() -> bytes:
    return Path(__file__).with_name("current_world_map_full_scan_probe.lua").read_bytes()


DAILY_RUNTIME_INSPECTION_ATTEMPTS = 4
DAILY_RUNTIME_INSPECTION_RETRY_SECONDS = 0.25


def _verified_daily_runtime_state(paths: dict[str, Path]) -> dict[str, object]:
    """Require the exact Daily Task runtime, tolerating only a transient read race."""
    state: dict[str, object] = {}
    for attempt in range(DAILY_RUNTIME_INSPECTION_ATTEMPTS):
        state = inspect_daily_task_runtime(paths)
        if state.get("installed") is True:
            return state
        if attempt + 1 < DAILY_RUNTIME_INSPECTION_ATTEMPTS:
            time.sleep(DAILY_RUNTIME_INSPECTION_RETRY_SECONDS)

    current_hashes = state.get("current_hashes")
    manifest = state.get("manifest")
    installed_hashes = manifest.get("installedHashes") if isinstance(manifest, dict) else None
    mismatched_hashes: list[str] = []
    if isinstance(current_hashes, dict) and isinstance(installed_hashes, dict):
        mismatched_hashes = [
            key for key in ("data", "metadata", "version", "baseutils")
            if current_hashes.get(key) != installed_hashes.get(key)
        ]
    diagnostics = (
        f"runtime_entry={state.get('runtime_entry_present')}, "
        f"preserved_original={state.get('preserved_original_present')}, "
        f"payloads_match={state.get('payloads_match_current_source')}, "
        f"manifest={state.get('manifest_present')}, "
        f"manifest_hash_match={state.get('manifest_hash_match')}, "
        f"mismatched_hashes={','.join(mismatched_hashes) or 'none'}"
    )
    raise InstallRefused(
        "Loader markers are present but they are not the exact verified Daily Task runtime "
        f"after {DAILY_RUNTIME_INSPECTION_ATTEMPTS} exact inspections ({diagnostics})"
    )


def _full_scan_wrapper_source(probe_source: bytes) -> bytes:
    """Embed the probe in LuaEntry; do not depend on an injected require name.

    PROVEN current-install loader constraint (2026-09-06): guarded candidates
    could load the already-present DataCenter.Global.LuaEntry_original, but both
    a newly appended LWControlProbe module and a replaced secondary runtime
    module were reported as "module ... not found". The modified LuaEntry itself
    did execute. Keep the only secondary require on the already-present preserved
    official LuaEntry and embed the full-scan module body directly here.
    """
    prefix = r'''-- LWCONTROL_WORLD_MAP_FULL_SCAN_LOADER
local unpack_values = table.unpack or unpack
local ok_original, original = pcall(require, "DataCenter.Global.LuaEntry_original")
if not ok_original then error(original) end
local probe = (function()
'''.encode("utf-8")
    suffix = r'''
end)()
if type(probe) ~= "table" then error("embedded full-scan probe did not return a table") end
pcall(probe.Pump)
local function wrap(name)
    if type(original) ~= "table" or type(original[name]) ~= "function" then return end
    local previous = original[name]
    original[name] = function(...)
        local values = { pcall(previous, ...) }
        local ok = table.remove(values, 1)
        pcall(probe.Pump)
        if not ok then error(values[1]) end
        return unpack_values(values)
    end
end
for _, method in ipairs({
    "init", "__InitCModule", "Async_Init", "Async_Update", "AsyncUpdate",
    "Update", "LateUpdate"
}) do
    wrap(method)
end
rawset(_G, "LWControlProbe", probe)
return original
'''.encode("utf-8")
    return prefix + probe_source + suffix


def _source_preflight(paths: dict[str, Path]) -> dict[str, object]:
    """Accept the official package or our exact installed Daily Task runtime.

    The Daily Task runtime intentionally preserves the official LuaEntry under
    ORIGINAL_LUA_ENTRY. Generic loader-probe preflight sees that marker without
    LWControlProbe and correctly refuses it as an unknown partial probe. For a
    full-scan candidate we can safely use that preserved official entry only
    after the Daily Task installer proves its own payloads and manifest hashes.
    """
    try:
        return preflight(paths)
    except InstallRefused as exc:
        if str(exc) != "Incomplete loader-probe installation markers are present":
            raise
    state = _verified_daily_runtime_state(paths)
    _, _, entries = read_lwlf(paths["data"])
    mapped = _entry_map(entries)
    official = mapped.get(ORIGINAL_LUA_ENTRY)
    if official is None:
        raise InstallRefused("Verified Daily Task runtime is missing preserved official LuaEntry")
    official_hash = hashlib.sha256(official).hexdigest()
    if official_hash != EXPECTED_LUA_ENTRY_SHA256:
        raise InstallRefused(
            f"Preserved official LuaEntry SHA-256 {official_hash} does not match {EXPECTED_LUA_ENTRY_SHA256}"
        )
    return {
        "file_version": state["file_version"],
        "content_version": state["content_version"],
        "entry_count": state["entry_count"],
        "lua_entry_sha256": official_hash,
        "already_installed": False,
        "source_runtime": "verified_daily_task_runtime",
        "source_runtime_hashes": state["current_hashes"],
    }


def _official_entry(paths: dict[str, Path], mapped: dict[str, bytes]) -> bytes:
    if ORIGINAL_LUA_ENTRY in mapped:
        _verified_daily_runtime_state(paths)
        official = mapped[ORIGINAL_LUA_ENTRY]
    else:
        official = mapped.get(LUA_ENTRY)
    if official is None:
        raise InstallRefused(f"{LUA_ENTRY} is missing from LWScripts.data")
    if hashlib.sha256(official).hexdigest() != EXPECTED_LUA_ENTRY_SHA256:
        raise InstallRefused("Selected official LuaEntry hash does not match the current-build contract")
    return bytes(official)


def _encoded_entries(paths: dict[str, Path], entries: list[tuple[str, bytes]]):
    mapped = _entry_map(entries)
    if ORIGINAL_LUA_ENTRY not in mapped:
        raise InstallRefused(
            "full-scan candidate requires the already-present preserved official LuaEntry; "
            "refusing to append an unproven module name"
        )
    official = _official_entry(paths, mapped)
    native = derive_xlua_key_nonce(_xlua_path(paths))
    probe_source = _probe_source()
    wrapper_source = _full_scan_wrapper_source(probe_source)
    wrapper = encode_lenc_bytes(wrapper_source, native["key"], native["nonce"])
    if decode_lenc_bytes(wrapper, native["key"], native["nonce"])["decoded"] != wrapper_source:
        raise InstallRefused("full-scan wrapper LENC round-trip failed")

    mapped[ORIGINAL_LUA_ENTRY] = bytes(official)
    mapped[LUA_ENTRY] = wrapper
    output: list[tuple[str, bytes]] = []
    for name, _ in entries:
        output.append((name, mapped[name]))
    return output, native, official, wrapper_source, probe_source


def prepare(paths: dict[str, Path], output_dir: Path) -> dict[str, object]:
    before = _source_preflight(paths)
    if before["file_version"] != 3:
        raise InstallRefused("full-scan probe requires current LWLF version 3")
    output_dir = output_dir.resolve()
    output_dir.mkdir(parents=True, exist_ok=True)
    outputs = [output_dir / "LWScripts.data", output_dir / "LWScripts.txt", output_dir / "version.txt"]
    if any(path.exists() for path in outputs):
        raise InstallRefused("full-scan candidate output already exists")

    file_version, content_version, entries = read_lwlf(paths["data"])
    prepared, native, official, wrapper_source, probe_source = _encoded_entries(paths, entries)
    write_lwlf(outputs[0], file_version, content_version, prepared)
    verify_version, verify_content, verify_entries = read_lwlf(outputs[0])
    verify_map = _entry_map(verify_entries)
    if (verify_version, verify_content) != (file_version, content_version):
        raise InstallRefused("full-scan candidate header did not round-trip")
    if verify_map.get(ORIGINAL_LUA_ENTRY) != official:
        raise InstallRefused("full-scan candidate did not preserve official LuaEntry")
    if decode_lenc_bytes(verify_map[LUA_ENTRY], native["key"], native["nonce"])["decoded"] != wrapper_source:
        raise InstallRefused("full-scan serialized wrapper verification failed")
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
        raise InstallRefused("full-scan candidate is incomplete")
    file_version, content_version, entries = read_lwlf(data)
    mapped = _entry_map(entries)
    native = derive_xlua_key_nonce(_xlua_path(paths))
    official_file_version, official_content_version, official_entries = read_lwlf(paths["data"])
    official_map = _entry_map(official_entries)
    official_entry = _official_entry(paths, official_map)
    if (file_version, content_version) != (official_file_version, official_content_version):
        raise InstallRefused("full-scan candidate LWLF version does not match installed build")
    if mapped.get(ORIGINAL_LUA_ENTRY) != official_entry:
        raise InstallRefused("full-scan candidate original LuaEntry does not match installed build")
    if decode_lenc_bytes(
        mapped[LUA_ENTRY], native["key"], native["nonce"]
    )["decoded"] != _full_scan_wrapper_source(_probe_source()):
        raise InstallRefused("full-scan candidate wrapper does not match expected source")
    expected_length, expected_crc = metadata.read_text(encoding="utf-8").strip().split("|", 1)
    if int(expected_length) != data.stat().st_size or int(expected_crc) != crc32_file(data):
        raise InstallRefused("full-scan candidate metadata length/CRC mismatch")
    if int(version.read_text(encoding="utf-8").strip()) != content_version:
        raise InstallRefused("full-scan candidate version.txt mismatch")
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
