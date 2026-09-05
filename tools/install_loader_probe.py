"""Version-aware Last War Lua loader probe installer.

This tool is intentionally narrow. It installs a tiny Lua heartbeat probe so the
reconstructed controller can prove that the current game build loads injected
scripts. It does not queue bridge commands or perform gameplay actions.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import os
from pathlib import Path
import shutil
import struct
import subprocess
import tempfile
import time
import uuid
import zlib

try:
    from .inspect_baseutils_rdl import inspect as inspect_baseutils
except ImportError:  # direct script execution: python tools/install_loader_probe.py
    from inspect_baseutils_rdl import inspect as inspect_baseutils


EXPECTED_CONTENT_VERSION = 12
EXPECTED_BASEUTILS_SHA256 = "b20865f42c9272e0fca2b6deb2e9142576b12dcf42f11ad1a8816ddd59b952d6"
FAILED_DEBUG_PATCH_SHA256 = "72b813dd3e36be894b4dafe4f1b58e19e60956ba7b9d1c0b2786e48b1e995798"
EXPECTED_LUA_ENTRY_SHA256 = "50f3ae906a8e9898549c4ea740eedc772a88eb2979e165eb35733192d100a137"
PROBE_VERSION = "lwcontrol-loader-probe-1"
LUA_ENTRY = "DataCenter/Global/LuaEntry.luac"
ORIGINAL_LUA_ENTRY = "DataCenter/Global/LuaEntry_original.luac"
PROBE_ENTRY = "LWControlProbe.luac"
APPLY_DISABLED_REASON = (
    "Loader-probe installation is disabled: the LWLF-v3 LENC encoder and an offline "
    "candidate-package path are recovered, but the candidate has not yet been "
    "validated by a bounded current-game load. Use --prepare-dir for read-only/offline "
    "package generation and verification."
)


class InstallRefused(RuntimeError):
    pass


def _read_7bit(stream) -> int:
    value = 0
    shift = 0
    for _ in range(5):
        raw = stream.read(1)
        if len(raw) != 1:
            raise EOFError("LWLF 7-bit integer ended early")
        byte = raw[0]
        value |= (byte & 0x7F) << shift
        if byte & 0x80 == 0:
            return value
        shift += 7
    raise ValueError("Invalid LWLF 7-bit integer")


def _write_7bit(stream, value: int) -> None:
    if value < 0:
        raise ValueError("LWLF length cannot be negative")
    remaining = value
    while remaining >= 0x80:
        stream.write(bytes(((remaining | 0x80) & 0xFF,)))
        remaining >>= 7
    stream.write(bytes((remaining,)))


def read_lwlf(path: Path) -> tuple[int, int, list[tuple[str, bytes]]]:
    with path.open("rb") as stream:
        if stream.read(4) != b"LWLF":
            raise InstallRefused("LWScripts.data is not an LWLF container")
        header = stream.read(12)
        if len(header) != 12:
            raise EOFError("LWLF header ended early")
        file_version, content_version, count = struct.unpack("<III", header)
        if count > 1_000_000:
            raise InstallRefused(f"Unreasonable LWLF entry count: {count}")
        entries: list[tuple[str, bytes]] = []
        names: set[str] = set()
        for _ in range(count):
            name_size = _read_7bit(stream)
            if name_size > 1_048_576:
                raise InstallRefused(f"Unreasonable LWLF name size: {name_size}")
            name_raw = stream.read(name_size)
            if len(name_raw) != name_size:
                raise EOFError("LWLF entry name ended early")
            name = name_raw.decode("utf-8").replace("\\", "/")
            size_raw = stream.read(4)
            if len(size_raw) != 4:
                raise EOFError(f"LWLF entry size ended early: {name}")
            size = struct.unpack("<I", size_raw)[0]
            data = stream.read(size)
            if len(data) != size:
                raise EOFError(f"LWLF entry ended early: {name}")
            if name in names:
                raise InstallRefused(f"Duplicate LWLF entry: {name}")
            names.add(name)
            entries.append((name, data))
        if stream.read(1):
            raise InstallRefused("Unexpected bytes remain after the LWLF entry table")
        return file_version, content_version, entries


def write_lwlf(path: Path, file_version: int, content_version: int,
               entries: list[tuple[str, bytes]]) -> None:
    names: set[str] = set()
    with path.open("wb") as stream:
        stream.write(b"LWLF")
        stream.write(struct.pack("<III", file_version, content_version, len(entries)))
        for raw_name, data in entries:
            name = raw_name.replace("\\", "/")
            if not name or name in names:
                raise InstallRefused(f"Invalid or duplicate LWLF entry: {name!r}")
            names.add(name)
            encoded = name.encode("utf-8")
            _write_7bit(stream, len(encoded))
            stream.write(encoded)
            stream.write(struct.pack("<I", len(data)))
            stream.write(data)


def crc32_file(path: Path) -> int:
    crc = 0
    with path.open("rb") as stream:
        while chunk := stream.read(1024 * 1024):
            crc = zlib.crc32(chunk, crc)
    return crc & 0xFFFFFFFF


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        while chunk := stream.read(1024 * 1024):
            digest.update(chunk)
    return digest.hexdigest()


def game_is_running() -> bool:
    if os.name != "nt":
        return False
    result = subprocess.run(
        ["tasklist", "/FI", "IMAGENAME eq LastWar.exe", "/FO", "CSV", "/NH"],
        capture_output=True, text=True, check=False,
    )
    return '"LastWar.exe"' in result.stdout


def discover_paths() -> dict[str, Path]:
    local = Path(os.environ["LOCALAPPDATA"]).resolve()
    local_low = (local.parent / "LocalLow").resolve()
    install = local / "FunFly" / "Last War-Survival Game"
    script_dir = local_low / "FunFly" / "Last War-Survival Game" / "lwScripts"
    return {
        "game_exe": install / "Game" / "LastWar.exe",
        "launcher": install / "LastWarLauncher.exe",
        "baseutils": install / "Game" / "LastWar_Data" / "Assemblies" / "BaseUtils.rdl",
        "script_dir": script_dir,
        "data": script_dir / "LWScripts.data",
        "metadata": script_dir / "LWScripts.txt",
        "version": script_dir / "version.txt",
        "runtime": local / "LWControl" / "runtime",
        "backup_root": local / "LWControl" / "backups",
    }


def _parse_metadata(path: Path) -> tuple[int, int]:
    text = path.read_text(encoding="utf-8").strip()
    parts = text.split("|")
    if len(parts) != 2:
        raise InstallRefused("LWScripts.txt must contain '<length>|<crc32>'")
    try:
        return int(parts[0]), int(parts[1])
    except ValueError as exc:
        raise InstallRefused("LWScripts.txt contains non-numeric metadata") from exc


def _entry_map(entries: list[tuple[str, bytes]]) -> dict[str, bytes]:
    return {name: data for name, data in entries}


def _xlua_path(paths: dict[str, Path]) -> Path:
    return paths["game_exe"].parent / "LastWar_Data" / "Plugins" / "x86_64" / "xlua.dll"


def _installed_probe_state(mapped: dict[str, bytes], xlua: Path) -> dict:
    """Recognize only the exact encrypted wrapper/probe pair produced by this tool."""
    has_original = ORIGINAL_LUA_ENTRY in mapped
    has_probe = PROBE_ENTRY in mapped
    if not has_original and not has_probe:
        return {"installed": False}
    if not has_original or not has_probe:
        raise InstallRefused("Incomplete loader-probe installation markers are present")

    try:
        from .extract_lenc_v3 import decode_lenc_bytes, derive_xlua_key_nonce
    except ImportError:
        from extract_lenc_v3 import decode_lenc_bytes, derive_xlua_key_nonce

    native = derive_xlua_key_nonce(xlua)
    try:
        wrapper = decode_lenc_bytes(mapped[LUA_ENTRY], native["key"], native["nonce"])
        probe = decode_lenc_bytes(mapped[PROBE_ENTRY], native["key"], native["nonce"])
    except (OSError, EOFError, ValueError, zlib.error) as exc:
        raise InstallRefused("Installed loader-probe entries are not valid current-build LENC payloads") from exc
    if wrapper["decoded"] != _entry_source() or probe["decoded"] != _probe_source():
        raise InstallRefused("A different encrypted LuaEntry/probe modification is already installed")

    official = mapped[ORIGINAL_LUA_ENTRY]
    official_hash = hashlib.sha256(official).hexdigest()
    if official_hash != EXPECTED_LUA_ENTRY_SHA256:
        raise InstallRefused(
            f"Preserved official LuaEntry SHA-256 {official_hash} does not match {EXPECTED_LUA_ENTRY_SHA256}"
        )
    return {
        "installed": True,
        "xlua_sha256": native["sha256"],
        "official_entry": official,
        "active_entry_sha256": hashlib.sha256(mapped[LUA_ENTRY]).hexdigest(),
        "probe_entry_sha256": hashlib.sha256(mapped[PROBE_ENTRY]).hexdigest(),
        "payloads_verified": True,
    }


def preflight(paths: dict[str, Path]) -> dict:
    if game_is_running():
        raise InstallRefused("LastWar is running; close it before installing the loader probe")
    for key in ("game_exe", "baseutils", "data", "metadata", "version"):
        if not paths[key].is_file():
            raise InstallRefused(f"Required file is missing: {paths[key]}")

    file_version, content_version, entries = read_lwlf(paths["data"])
    if content_version != EXPECTED_CONTENT_VERSION:
        raise InstallRefused(
            f"Unsupported script content version {content_version}; expected {EXPECTED_CONTENT_VERSION}"
        )
    try:
        version_text = int(paths["version"].read_text(encoding="utf-8").strip())
    except ValueError as exc:
        raise InstallRefused("version.txt is not an integer") from exc
    if version_text != content_version:
        raise InstallRefused(
            f"version.txt ({version_text}) does not match LWScripts.data ({content_version})"
        )

    expected_length, expected_crc = _parse_metadata(paths["metadata"])
    actual_length = paths["data"].stat().st_size
    actual_crc = crc32_file(paths["data"])
    if (expected_length, expected_crc) != (actual_length, actual_crc):
        raise InstallRefused(
            "LWScripts.txt integrity metadata does not match the current package "
            f"(expected {expected_length}|{expected_crc}, actual {actual_length}|{actual_crc})"
        )

    base_hash = sha256_file(paths["baseutils"])
    if base_hash == FAILED_DEBUG_PATCH_SHA256:
        raise InstallRefused(
            "BaseUtils.rdl is in the rejected IsDebug=true probe state; restore the clean backup first"
        )
    if base_hash != EXPECTED_BASEUTILS_SHA256:
        raise InstallRefused(
            f"Unsupported BaseUtils.rdl SHA-256 {base_hash}; expected {EXPECTED_BASEUTILS_SHA256}"
        )
    rdl = inspect_baseutils(paths["baseutils"])
    method = rdl["method"]
    declaring = method["declaring_type"]
    if declaring["name"] != "CommonUtils" or declaring["namespace"] != "":
        raise InstallRefused("Resolved IsDebug method is not CommonUtils.IsDebug")
    if method["signature"] != "00 00 02" or not method["legacy_installer_signature_match"]:
        raise InstallRefused("CommonUtils.IsDebug no longer has the recovered Boolean tiny-method contract")
    if method["constant_boolean_return"] is not False:
        raise InstallRefused("Clean BaseUtils.rdl hash does not contain the expected false IsDebug constant")

    mapped = _entry_map(entries)
    if LUA_ENTRY not in mapped:
        raise InstallRefused(f"{LUA_ENTRY} is missing from LWScripts.data")
    probe_state = _installed_probe_state(mapped, _xlua_path(paths))
    installed = probe_state["installed"]
    official_entry = probe_state.get("official_entry", mapped[LUA_ENTRY])
    official_hash = hashlib.sha256(official_entry).hexdigest()
    if official_hash != EXPECTED_LUA_ENTRY_SHA256:
        raise InstallRefused(
            f"Official LuaEntry SHA-256 {official_hash} does not match {EXPECTED_LUA_ENTRY_SHA256}"
        )

    result = {
        "file_version": file_version,
        "content_version": content_version,
        "entry_count": len(entries),
        "package_bytes": actual_length,
        "package_crc32": actual_crc,
        "baseutils_sha256": base_hash,
        "baseutils_state": "clean",
        "is_debug_file_offset": method["file_offset"],
        "is_debug_rva": method["rva"],
        "is_debug_value": method["constant_boolean_return"],
        "lua_entry_bytes": len(official_entry),
        "lua_entry_sha256": official_hash,
        "lua_entry_prefix_hex": official_entry[:32].hex(),
        "already_installed": installed,
    }
    if installed:
        result.update({
            "active_lua_entry_sha256": probe_state["active_entry_sha256"],
            "probe_entry_sha256": probe_state["probe_entry_sha256"],
            "probe_payloads_verified": probe_state["payloads_verified"],
            "xlua_sha256": probe_state["xlua_sha256"],
        })
    return result


def _entry_source() -> bytes:
    source = r'''-- LWCONTROL_LOADER_PROBE
local unpack_values = table.unpack or unpack
local ok_original, original = pcall(require, "DataCenter.Global.LuaEntry_original")
if not ok_original then error(original) end
local ok_probe, probe = pcall(require, "LWControlProbe")
if not ok_probe then error(probe) end
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
'''
    return source.encode("utf-8")


def _probe_source() -> bytes:
    source = f'''local M = {{ VERSION = "{PROBE_VERSION}" }}
local root = (os.getenv("LOCALAPPDATA") or ".") .. [[\\LWControl\\runtime]]
local path = root .. [[\\loader-probe.json]]
local last_write = 0
local function write_probe()
    local now = tonumber(os.time()) or 0
    if now == last_write then return true end
    local file = io.open(path, "wb")
    if not file then return false end
    file:write('{{"version":"{PROBE_VERSION}","loaded":true,"updated_at":', tostring(now), '}}')
    file:close()
    last_write = now
    return true
end
function M.Pump()
    return write_probe()
end
write_probe()
return M
'''
    return source.encode("utf-8")


def _prepare_entries(entries: list[tuple[str, bytes]]) -> list[tuple[str, bytes]]:
    mapped = _entry_map(entries)
    if ORIGINAL_LUA_ENTRY not in mapped:
        mapped[ORIGINAL_LUA_ENTRY] = mapped[LUA_ENTRY]
    mapped[LUA_ENTRY] = _entry_source()
    mapped[PROBE_ENTRY] = _probe_source()

    output: list[tuple[str, bytes]] = []
    seen: set[str] = set()
    for name, _ in entries:
        output.append((name, mapped[name]))
        seen.add(name)
    for name in (ORIGINAL_LUA_ENTRY, PROBE_ENTRY):
        if name not in seen:
            output.append((name, mapped[name]))
    return output


def _prepare_lenc_entries(entries: list[tuple[str, bytes]], xlua: Path) -> tuple[list[tuple[str, bytes]], dict]:
    """Prepare current-build encrypted probe entries without writing game files."""
    try:
        from .extract_lenc_v3 import decode_lenc_bytes, derive_xlua_key_nonce, encode_lenc_bytes
    except ImportError:
        from extract_lenc_v3 import decode_lenc_bytes, derive_xlua_key_nonce, encode_lenc_bytes

    mapped = _entry_map(entries)
    if LUA_ENTRY not in mapped:
        raise InstallRefused(f"{LUA_ENTRY} is missing from LWScripts.data")
    official = bytes(mapped[LUA_ENTRY])
    native = derive_xlua_key_nonce(xlua)
    key = native["key"]
    nonce = native["nonce"]
    wrapper = encode_lenc_bytes(_entry_source(), key, nonce)
    probe = encode_lenc_bytes(_probe_source(), key, nonce)

    mapped[ORIGINAL_LUA_ENTRY] = official
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

    wrapper_check = decode_lenc_bytes(wrapper, key, nonce)
    probe_check = decode_lenc_bytes(probe, key, nonce)
    if wrapper_check["decoded"] != _entry_source() or probe_check["decoded"] != _probe_source():
        raise InstallRefused("LENC probe entries failed encode/decode verification")
    return output, {
        "xlua_sha256": native["sha256"],
        "wrapper_size": len(wrapper),
        "probe_size": len(probe),
        "wrapper_sha256": hashlib.sha256(wrapper).hexdigest(),
        "probe_sha256": hashlib.sha256(probe).hexdigest(),
        "official_preserved_sha256": hashlib.sha256(official).hexdigest(),
        "round_trip_verified": True,
    }


def prepare_offline_candidate(paths: dict[str, Path], output_dir: Path) -> dict:
    """Build and verify a separate candidate package; never replace installed files."""
    before = preflight(paths)
    if before["file_version"] != 3:
        raise InstallRefused(f"offline LENC preparation requires LWLF version 3, got {before['file_version']}")
    output_dir = output_dir.resolve()
    output_dir.mkdir(parents=True, exist_ok=True)
    data_out = output_dir / "LWScripts.data"
    metadata_out = output_dir / "LWScripts.txt"
    version_out = output_dir / "version.txt"
    for path in (data_out, metadata_out, version_out):
        if path.exists():
            raise InstallRefused(f"offline candidate output already exists: {path}")

    xlua = _xlua_path(paths)
    file_version, content_version, entries = read_lwlf(paths["data"])
    prepared_entries, lenc = _prepare_lenc_entries(entries, xlua)
    write_lwlf(data_out, file_version, content_version, prepared_entries)
    verify_version, verify_content, verify_entries = read_lwlf(data_out)
    verify_map = _entry_map(verify_entries)
    if (verify_version, verify_content) != (file_version, content_version):
        raise InstallRefused("offline candidate LWLF header did not round-trip")
    if verify_map.get(ORIGINAL_LUA_ENTRY) != _entry_map(entries)[LUA_ENTRY]:
        raise InstallRefused("offline candidate did not preserve the official LuaEntry bytes")
    try:
        from .extract_lenc_v3 import decode_lenc_bytes, derive_xlua_key_nonce
    except ImportError:
        from extract_lenc_v3 import decode_lenc_bytes, derive_xlua_key_nonce
    native = derive_xlua_key_nonce(xlua)
    wrapper_check = decode_lenc_bytes(verify_map[LUA_ENTRY], native["key"], native["nonce"])
    probe_check = decode_lenc_bytes(verify_map[PROBE_ENTRY], native["key"], native["nonce"])
    if wrapper_check["decoded"] != _entry_source():
        raise InstallRefused("serialized offline candidate wrapper failed LENC decode verification")
    if probe_check["decoded"] != _probe_source():
        raise InstallRefused("serialized offline candidate probe failed LENC decode verification")
    package_crc = crc32_file(data_out)
    package_length = data_out.stat().st_size
    metadata_out.write_text(f"{package_length}|{package_crc}", encoding="utf-8")
    version_out.write_text(str(content_version), encoding="utf-8")
    return {
        "changed_installed_files": False,
        "output_dir": str(output_dir),
        "data": str(data_out),
        "metadata": str(metadata_out),
        "version": str(version_out),
        "file_version": file_version,
        "content_version": content_version,
        "entry_count": len(verify_entries),
        "package_length": package_length,
        "package_crc32": package_crc,
        "package_sha256": hashlib.sha256(data_out.read_bytes()).hexdigest(),
        "lenc": lenc,
        "serialized_round_trip_verified": True,
        "preflight": before,
    }


def verify_offline_candidate(paths: dict[str, Path], candidate_dir: Path) -> dict:
    """Run the installed-package preflight contract against a separate candidate directory."""
    before = preflight(paths)
    candidate_dir = candidate_dir.resolve()
    candidate_paths = dict(paths)
    candidate_paths["data"] = candidate_dir / "LWScripts.data"
    candidate_paths["metadata"] = candidate_dir / "LWScripts.txt"
    candidate_paths["version"] = candidate_dir / "version.txt"
    for key in ("data", "metadata", "version"):
        if not candidate_paths[key].is_file():
            raise InstallRefused(f"Candidate is missing {candidate_paths[key].name}: {candidate_paths[key]}")

    candidate = preflight(candidate_paths)
    if not candidate["already_installed"] or not candidate.get("probe_payloads_verified"):
        raise InstallRefused("Candidate does not contain the exact encrypted loader-probe payloads")
    if candidate["lua_entry_sha256"] != before["lua_entry_sha256"]:
        raise InstallRefused("Candidate did not preserve the current official LuaEntry")
    return {
        "changed_installed_files": False,
        "candidate_dir": str(candidate_dir),
        "installed_preflight": before,
        "candidate_preflight": candidate,
    }


def _make_backup(paths: dict[str, Path]) -> Path:
    root = paths["backup_root"]
    root.mkdir(parents=True, exist_ok=True)
    backup = root / f"loader-probe-{time.strftime('%Y%m%d-%H%M%S', time.gmtime())}-{uuid.uuid4().hex}"
    backup.mkdir()
    for key in ("data", "metadata", "version", "baseutils"):
        shutil.copy2(paths[key], backup / paths[key].name)
    return backup


def _temporary_path(directory: Path, prefix: str, suffix: str) -> Path:
    descriptor, name = tempfile.mkstemp(prefix=prefix, suffix=suffix, dir=directory)
    os.close(descriptor)
    return Path(name)


def _restore_from_backup(paths: dict[str, Path], backup: Path) -> None:
    if game_is_running():
        raise InstallRefused("LastWar is running; close it before restoring game files")
    for key in ("data", "metadata", "version", "baseutils"):
        source = backup / paths[key].name
        if not source.is_file():
            raise InstallRefused(f"Backup is missing {source.name}")
    for key in ("data", "metadata", "version", "baseutils"):
        shutil.copy2(backup / paths[key].name, paths[key])


def apply_install(paths: dict[str, Path]) -> dict:
    raise InstallRefused(APPLY_DISABLED_REASON)


def _apply_install_legacy_research(paths: dict[str, Path]) -> dict:
    """Retained as offline research history; command-line apply never invokes it."""
    before = preflight(paths)
    if before["already_installed"] and before["is_debug_value"] is True:
        return {"changed": False, "preflight": before, "reason": "already_installed"}

    backup = _make_backup(paths)
    paths["runtime"].mkdir(parents=True, exist_ok=True)
    file_version, content_version, entries = read_lwlf(paths["data"])
    prepared_entries = _prepare_entries(entries)

    data_temp = _temporary_path(paths["script_dir"], "LWScripts-", ".data.tmp")
    metadata_temp = _temporary_path(paths["script_dir"], "LWScripts-", ".txt.tmp")
    try:
        write_lwlf(data_temp, file_version, content_version, prepared_entries)
        verify_file_version, verify_content_version, verify_entries = read_lwlf(data_temp)
        verify_map = _entry_map(verify_entries)
        if verify_file_version != file_version or verify_content_version != content_version:
            raise InstallRefused("Prepared LWLF header did not round-trip")
        if verify_map.get(LUA_ENTRY) != _entry_source() or verify_map.get(PROBE_ENTRY) != _probe_source():
            raise InstallRefused("Prepared loader-probe payload did not round-trip")
        package_crc = crc32_file(data_temp)
        package_length = data_temp.stat().st_size
        metadata_temp.write_text(f"{package_length}|{package_crc}", encoding="utf-8")

        try:
            os.replace(data_temp, paths["data"])
            os.replace(metadata_temp, paths["metadata"])
        except Exception:
            _restore_from_backup(paths, backup)
            raise

        after = preflight(paths)
        if after["is_debug_value"] is not False or not after["already_installed"]:
            _restore_from_backup(paths, backup)
            raise InstallRefused("Post-install verification failed; original files were restored")
        return {
            "changed": True,
            "backup": str(backup),
            "preflight": before,
            "after": after,
            "baseutils_changed": False,
            "installed_entries": [LUA_ENTRY, ORIGINAL_LUA_ENTRY, PROBE_ENTRY],
        }
    finally:
        for path in (data_temp, metadata_temp):
            try:
                path.unlink(missing_ok=True)
            except OSError:
                pass


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    mode = parser.add_mutually_exclusive_group()
    mode.add_argument(
        "--apply",
        action="store_true",
        help="fail closed until the prepared current-build candidate is validated live",
    )
    mode.add_argument("--restore", type=Path, help="restore a backup directory created by this tool")
    mode.add_argument(
        "--prepare-dir",
        type=Path,
        help="build and verify a separate encrypted candidate package without changing installed files",
    )
    mode.add_argument(
        "--verify-dir",
        type=Path,
        help="run the installed-package preflight contract against a separate encrypted candidate directory",
    )
    parser.add_argument("--json", action="store_true", help="emit JSON")
    args = parser.parse_args()
    paths = discover_paths()
    try:
        if args.restore:
            _restore_from_backup(paths, args.restore.resolve())
            result = {"restored": str(args.restore.resolve()), "preflight": preflight(paths)}
        elif args.prepare_dir:
            result = prepare_offline_candidate(paths, args.prepare_dir)
        elif args.verify_dir:
            result = verify_offline_candidate(paths, args.verify_dir)
        elif args.apply:
            result = apply_install(paths)
        else:
            result = {"dry_run": True, "preflight": preflight(paths)}
    except (InstallRefused, OSError, EOFError, ValueError) as exc:
        result = {"ok": False, "error": str(exc), "error_type": type(exc).__name__}
        print(json.dumps(result, indent=2) if args.json else f"REFUSED: {exc}")
        return 2
    result = {"ok": True, **result}
    print(json.dumps(result, indent=2) if args.json else result)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
