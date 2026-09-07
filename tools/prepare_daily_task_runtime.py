"""Build/verify an encrypted persistent Daily Task Claim runtime package offline."""

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
        _entry_map,
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
        _entry_map,
        _xlua_path,
        crc32_file,
        discover_paths,
        preflight,
        read_lwlf,
        write_lwlf,
    )


RUNTIME_ENTRY = "LWControlDailyTaskRuntime.luac"
WORLD_SCAN_ENTRY = "LWControlWorldScanRuntime.luac"
MARKER = "LWCONTROL_DAILY_TASK_RUNTIME"


def _builder_source() -> str:
    return Path(__file__).with_name("current_daily_task_snapshot_probe.lua").read_text(encoding="utf-8")


def _runtime_source() -> bytes:
    source = Path(__file__).with_name("lwcontrol_daily_task_runtime.lua").read_text(encoding="utf-8")
    return source.replace("__SNAPSHOT_BUILDER__", _builder_source()).encode("utf-8")


def _world_scan_source() -> bytes:
    return Path(__file__).with_name("current_world_map_full_scan_probe.lua").read_bytes()


def _wrapper_source() -> bytes:
    return f'''-- {MARKER}\nlocal unpack_values = table.unpack or unpack\nlocal ok_original, original = pcall(require, "DataCenter.Global.LuaEntry_original")\nif not ok_original then error(original) end\nlocal ok_runtime, runtime = pcall(require, "LWControlDailyTaskRuntime")\nif not ok_runtime then error(runtime) end\nrawset(_G, "LWControlWorldScanPersistentRuntime", true)\nlocal ok_world_scan, world_scan = pcall(require, "LWControlWorldScanRuntime")\nif not ok_world_scan then error(world_scan) end\npcall(runtime.Register)\npcall(world_scan.Register)\nlocal function wrap(name)\n    if type(original) ~= "table" or type(original[name]) ~= "function" then return end\n    local previous = original[name]\n    original[name] = function(...)\n        local values = {{ pcall(previous, ...) }}\n        local ok = table.remove(values, 1)\n        pcall(runtime.Pump)\n        pcall(world_scan.Pump)\n        if not ok then error(values[1]) end\n        return unpack_values(values)\n    end\nend\nfor _, method in ipairs({{"init", "__InitCModule", "Async_Init", "Async_Update", "AsyncUpdate", "Update", "LateUpdate"}}) do wrap(method) end\nrawset(_G, "LWControlDailyTaskRuntime", runtime)\nrawset(_G, "LWControlWorldScanRuntime", world_scan)\nreturn original\n'''.encode("utf-8")


def _encoded_entries(paths: dict[str, Path], entries: list[tuple[str, bytes]]):
    mapped = _entry_map(entries)
    official = mapped.get(LUA_ENTRY)
    if official is None:
        raise InstallRefused(f"{LUA_ENTRY} is missing from LWScripts.data")
    if ORIGINAL_LUA_ENTRY in mapped or RUNTIME_ENTRY in mapped or WORLD_SCAN_ENTRY in mapped:
        raise InstallRefused("A loader/runtime modification is already present; start from the official package")

    native = derive_xlua_key_nonce(_xlua_path(paths))
    wrapper_source = _wrapper_source()
    runtime_source = _runtime_source()
    world_scan_source = _world_scan_source()
    wrapper = encode_lenc_bytes(wrapper_source, native["key"], native["nonce"])
    runtime = encode_lenc_bytes(runtime_source, native["key"], native["nonce"])
    world_scan = encode_lenc_bytes(world_scan_source, native["key"], native["nonce"])
    if decode_lenc_bytes(wrapper, native["key"], native["nonce"])["decoded"] != wrapper_source:
        raise InstallRefused("Daily runtime wrapper LENC round-trip failed")
    if decode_lenc_bytes(runtime, native["key"], native["nonce"])["decoded"] != runtime_source:
        raise InstallRefused("Daily runtime payload LENC round-trip failed")
    if decode_lenc_bytes(world_scan, native["key"], native["nonce"])["decoded"] != world_scan_source:
        raise InstallRefused("World Scan runtime payload LENC round-trip failed")

    mapped[ORIGINAL_LUA_ENTRY] = bytes(official)
    mapped[LUA_ENTRY] = wrapper
    mapped[RUNTIME_ENTRY] = runtime
    mapped[WORLD_SCAN_ENTRY] = world_scan
    output: list[tuple[str, bytes]] = []
    seen: set[str] = set()
    for name, _ in entries:
        output.append((name, mapped[name]))
        seen.add(name)
    for name in (ORIGINAL_LUA_ENTRY, RUNTIME_ENTRY, WORLD_SCAN_ENTRY):
        if name not in seen:
            output.append((name, mapped[name]))
    return output, native, bytes(official), wrapper_source, runtime_source, world_scan_source


def prepare(paths: dict[str, Path], output_dir: Path) -> dict[str, object]:
    before = preflight(paths)
    if before["file_version"] != 3:
        raise InstallRefused("Daily runtime requires current LWLF version 3")
    output_dir = output_dir.resolve()
    output_dir.mkdir(parents=True, exist_ok=True)
    outputs = [output_dir / "LWScripts.data", output_dir / "LWScripts.txt", output_dir / "version.txt"]
    if any(path.exists() for path in outputs):
        raise InstallRefused("Daily runtime candidate output already exists")

    file_version, content_version, entries = read_lwlf(paths["data"])
    prepared, native, official, wrapper_source, runtime_source, world_scan_source = _encoded_entries(paths, entries)
    write_lwlf(outputs[0], file_version, content_version, prepared)
    verify_version, verify_content, verify_entries = read_lwlf(outputs[0])
    verify_map = _entry_map(verify_entries)
    if (verify_version, verify_content) != (file_version, content_version):
        raise InstallRefused("Daily runtime candidate header did not round-trip")
    if verify_map.get(ORIGINAL_LUA_ENTRY) != official:
        raise InstallRefused("Daily runtime candidate did not preserve official LuaEntry")
    wrapper_decoded = decode_lenc_bytes(verify_map[LUA_ENTRY], native["key"], native["nonce"])["decoded"]
    runtime_decoded = decode_lenc_bytes(verify_map[RUNTIME_ENTRY], native["key"], native["nonce"])["decoded"]
    world_scan_decoded = decode_lenc_bytes(verify_map[WORLD_SCAN_ENTRY], native["key"], native["nonce"])["decoded"]
    if wrapper_decoded != wrapper_source or runtime_decoded != runtime_source or world_scan_decoded != world_scan_source:
        raise InstallRefused("Daily runtime serialized payload verification failed")

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
        "wrapper_source_sha256": hashlib.sha256(wrapper_source).hexdigest(),
        "runtime_source_sha256": hashlib.sha256(runtime_source).hexdigest(),
        "world_scan_source_sha256": hashlib.sha256(world_scan_source).hexdigest(),
        "builder_source_sha256": hashlib.sha256(_builder_source().encode("utf-8")).hexdigest(),
        "xlua_sha256": native["sha256"],
        "serialized_round_trip_verified": True,
        "official_preflight": before,
    }


def verify(paths: dict[str, Path], candidate_dir: Path) -> dict[str, object]:
    before = preflight(paths)
    candidate_dir = candidate_dir.resolve()
    data = candidate_dir / "LWScripts.data"
    metadata = candidate_dir / "LWScripts.txt"
    version = candidate_dir / "version.txt"
    for path in (data, metadata, version):
        if not path.is_file():
            raise InstallRefused(f"Candidate is missing {path.name}")
    file_version, content_version, entries = read_lwlf(data)
    mapped = _entry_map(entries)
    native = derive_xlua_key_nonce(_xlua_path(paths))
    if file_version != before["file_version"] or content_version != before["content_version"]:
        raise InstallRefused("Daily runtime candidate version does not match installed game")
    if mapped.get(ORIGINAL_LUA_ENTRY) != _entry_map(read_lwlf(paths["data"])[2]).get(LUA_ENTRY):
        raise InstallRefused("Daily runtime preserved LuaEntry no longer matches installed game")
    wrapper = decode_lenc_bytes(mapped[LUA_ENTRY], native["key"], native["nonce"])["decoded"]
    runtime = decode_lenc_bytes(mapped[RUNTIME_ENTRY], native["key"], native["nonce"])["decoded"]
    world_scan = decode_lenc_bytes(mapped[WORLD_SCAN_ENTRY], native["key"], native["nonce"])["decoded"]
    if wrapper != _wrapper_source() or runtime != _runtime_source() or world_scan != _world_scan_source():
        raise InstallRefused("Daily runtime candidate payloads do not match current source")
    expected_meta = f"{data.stat().st_size}|{crc32_file(data)}"
    if metadata.read_text(encoding="utf-8").strip() != expected_meta:
        raise InstallRefused("Daily runtime candidate metadata mismatch")
    if version.read_text(encoding="utf-8").strip() != str(content_version):
        raise InstallRefused("Daily runtime candidate version.txt mismatch")
    return {
        "changed_installed_files": False,
        "candidate_dir": str(candidate_dir),
        "content_version": content_version,
        "entry_count": len(entries),
        "package_sha256": hashlib.sha256(data.read_bytes()).hexdigest(),
        "payloads_verified": True,
        "official_preflight": before,
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
