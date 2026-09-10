#!/usr/bin/env python3
"""Run one bounded fresh resource acquisition against the verified current client.

This is the LWBridge rebuild's first-live-result adapter.  It reuses the
LWB-R7-001 loader/package mechanics and current WorldPointManager source, but it
does not claim to implement the original LWBridge named-pipe request grammar.
The command-file rendezvous and time bounds are explicit rebuild policy.
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
import sys
import tempfile
import time
import uuid
import zlib


PROBE_VERSION = "lwbridge-live-resource-probe-1"
LUA_ENTRY = "DataCenter/Global/LuaEntry.luac"
ORIGINAL_LUA_ENTRY = "DataCenter/Global/LuaEntry_original.luac"
EXPECTED_FILE_VERSION = 3
EXPECTED_CONTENT_VERSION = 14
EXPECTED_PACKAGE_SHA256 = "09ddc4d1727bc0676ef6320db79814852cacc5c82b53551c703722052ebdbace"
EXPECTED_PACKAGE_SIZE = 41269242
EXPECTED_PACKAGE_CRC32 = 3541420783
EXPECTED_LUA_ENTRY_SHA256 = "50f3ae906a8e9898549c4ea740eedc772a88eb2979e165eb35733192d100a137"
EXPECTED_XLUA_SHA256 = "21eb704afdb7e528f4b90fa1b90bf414c221b06ba990d625aaaaed31b292740f"
EXPECTED_ASSEMBLY_CSHARP_SHA256 = "871efe06819fbac438413eb96b7df8193d0be56094f3a44d5ff141e6219adcbd"

# RECOVERED current xLua LENC v3 contract, gated by EXPECTED_XLUA_SHA256.
LENC_KEY = bytes.fromhex("e916bd5e0105ffd6514ca6d01177e39d26eaca762d9cbb899b6a1cdfa4f43255")
LENC_NONCE = bytes.fromhex("835e212a03453039b83ee25a")
LENC_MAGIC = b"LENC"


class LiveResourceError(RuntimeError):
    pass


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def crc32_file(path: Path) -> int:
    value = 0
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            value = zlib.crc32(chunk, value)
    return value & 0xFFFFFFFF


def read_7bit(stream) -> int:
    value = 0
    shift = 0
    for _ in range(5):
        raw = stream.read(1)
        if len(raw) != 1:
            raise LiveResourceError("LWLF 7-bit integer ended early")
        byte = raw[0]
        value |= (byte & 0x7F) << shift
        if byte & 0x80 == 0:
            return value
        shift += 7
    raise LiveResourceError("invalid LWLF 7-bit integer")


def write_7bit(stream, value: int) -> None:
    while value >= 0x80:
        stream.write(bytes(((value | 0x80) & 0xFF,)))
        value >>= 7
    stream.write(bytes((value,)))


def read_lwlf(path: Path) -> tuple[int, int, list[tuple[str, bytes]]]:
    with path.open("rb") as stream:
        if stream.read(4) != b"LWLF":
            raise LiveResourceError("LWScripts.data is not an LWLF container")
        header = stream.read(12)
        if len(header) != 12:
            raise LiveResourceError("LWLF header ended early")
        file_version, content_version, count = struct.unpack("<III", header)
        if count > 1_000_000:
            raise LiveResourceError("LWLF entry count is outside the bounded limit")
        entries: list[tuple[str, bytes]] = []
        names: set[str] = set()
        for _ in range(count):
            name_size = read_7bit(stream)
            name_raw = stream.read(name_size)
            if len(name_raw) != name_size:
                raise LiveResourceError("LWLF entry name ended early")
            name = name_raw.decode("utf-8").replace("\\", "/")
            size_raw = stream.read(4)
            if len(size_raw) != 4:
                raise LiveResourceError(f"LWLF entry size ended early: {name}")
            size = struct.unpack("<I", size_raw)[0]
            data = stream.read(size)
            if len(data) != size:
                raise LiveResourceError(f"LWLF entry ended early: {name}")
            if name in names:
                raise LiveResourceError(f"duplicate LWLF entry: {name}")
            names.add(name)
            entries.append((name, data))
        if stream.read(1):
            raise LiveResourceError("unexpected bytes remain after LWLF entry table")
        return file_version, content_version, entries


def write_lwlf(path: Path, file_version: int, content_version: int,
               entries: list[tuple[str, bytes]]) -> None:
    with path.open("wb") as stream:
        stream.write(b"LWLF")
        stream.write(struct.pack("<III", file_version, content_version, len(entries)))
        for name, data in entries:
            encoded = name.replace("\\", "/").encode("utf-8")
            write_7bit(stream, len(encoded))
            stream.write(encoded)
            stream.write(struct.pack("<I", len(data)))
            stream.write(data)


def rotl32(value: int, shift: int) -> int:
    return ((value << shift) & 0xFFFFFFFF) | (value >> (32 - shift))


def quarter_round(state: list[int], a: int, b: int, c: int, d: int) -> None:
    state[a] = (state[a] + state[b]) & 0xFFFFFFFF
    state[d] = rotl32(state[d] ^ state[a], 16)
    state[c] = (state[c] + state[d]) & 0xFFFFFFFF
    state[b] = rotl32(state[b] ^ state[c], 12)
    state[a] = (state[a] + state[b]) & 0xFFFFFFFF
    state[d] = rotl32(state[d] ^ state[a], 8)
    state[c] = (state[c] + state[d]) & 0xFFFFFFFF
    state[b] = rotl32(state[b] ^ state[c], 7)


def lenc_block(counter: int) -> bytes:
    state = list(struct.unpack("<4I", b"expand 32-byte k"))
    state.extend(struct.unpack("<8I", LENC_KEY))
    state.append(counter)
    state.extend(struct.unpack("<3I", LENC_NONCE))
    for _ in range(4):
        quarter_round(state, 0, 4, 8, 12)
        quarter_round(state, 1, 5, 9, 13)
        quarter_round(state, 2, 6, 10, 14)
        quarter_round(state, 3, 7, 11, 15)
        quarter_round(state, 0, 5, 10, 15)
        quarter_round(state, 1, 6, 11, 12)
        quarter_round(state, 2, 7, 8, 13)
        quarter_round(state, 3, 4, 9, 14)
    return struct.pack("<16I", *state)


def lenc_transform(payload: bytes) -> bytes:
    output = bytearray(len(payload))
    for offset in range(0, len(payload), 64):
        stream = lenc_block(offset // 64)
        block = payload[offset: offset + 64]
        output[offset: offset + len(block)] = bytes(a ^ b for a, b in zip(block, stream))
    return bytes(output)


def encode_lenc(plaintext: bytes) -> bytes:
    compressed = zlib.compress(plaintext, 9)
    if not compressed.startswith(b"\x78\xDA"):
        raise LiveResourceError("probe compression did not produce the recovered 78DA LENC input")
    return LENC_MAGIC + lenc_transform(compressed)


def decode_lenc(entry: bytes) -> bytes:
    if not entry.startswith(LENC_MAGIC):
        raise LiveResourceError("candidate LuaEntry does not begin with LENC")
    transformed = lenc_transform(entry[4:])
    return zlib.decompress(transformed) if transformed.startswith(b"\x78\xDA") else transformed


def paths() -> dict[str, Path]:
    local = Path(os.environ["LOCALAPPDATA"]).resolve()
    profile = Path(os.environ["USERPROFILE"]).resolve()
    install = local / "FunFly" / "Last War-Survival Game"
    scripts = profile / "AppData" / "LocalLow" / "FunFly" / "Last War-Survival Game" / "lwScripts"
    return {
        "launcher": install / "LastWarLauncher.exe",
        "game": install / "Game" / "LastWar.exe",
        "xlua": install / "Game" / "LastWar_Data" / "Plugins" / "x86_64" / "xlua.dll",
        "assembly": install / "Game" / "LastWar_Data" / "Assemblies" / "Assembly-CSharp.rdl",
        "data": scripts / "LWScripts.data",
        "metadata": scripts / "LWScripts.txt",
        "version": scripts / "version.txt",
        "runtime": local / "LWBridgeRebuild" / "live-resource",
        "backup_root": local / "LWBridgeRebuild" / "live-resource-backups",
    }


def process_running(image: str) -> bool:
    result = subprocess.run(
        ["tasklist", "/FI", f"IMAGENAME eq {image}", "/FO", "CSV", "/NH"],
        capture_output=True, text=True, check=False,
    )
    return f'"{image}"' in result.stdout


def stop_game() -> None:
    if not process_running("LastWar.exe"):
        return
    subprocess.run(["taskkill", "/F", "/IM", "LastWar.exe"], capture_output=True, check=False)
    deadline = time.monotonic() + 12
    while process_running("LastWar.exe") and time.monotonic() < deadline:
        time.sleep(0.25)
    if process_running("LastWar.exe"):
        raise LiveResourceError("LastWar.exe did not stop for the bounded probe install")


def verify_current(p: dict[str, Path]) -> dict[str, object]:
    for key in ("launcher", "game", "xlua", "assembly", "data", "metadata", "version"):
        if not p[key].is_file():
            raise LiveResourceError(f"required current-client file is missing: {p[key]}")
    if sha256_file(p["xlua"]) != EXPECTED_XLUA_SHA256:
        raise LiveResourceError("current xlua.dll no longer matches the recovered LENC contract")
    if sha256_file(p["assembly"]) != EXPECTED_ASSEMBLY_CSHARP_SHA256:
        raise LiveResourceError("current Assembly-CSharp.rdl no longer matches the proven WorldPointManager contract")
    package_hash = sha256_file(p["data"])
    if package_hash != EXPECTED_PACKAGE_SHA256 or p["data"].stat().st_size != EXPECTED_PACKAGE_SIZE:
        raise LiveResourceError("current LWScripts.data no longer matches the live-proven v14 source package")
    if crc32_file(p["data"]) != EXPECTED_PACKAGE_CRC32:
        raise LiveResourceError("current LWScripts.data CRC no longer matches the live-proven v14 package")
    metadata = p["metadata"].read_text(encoding="utf-8-sig").strip()
    if metadata != f"{EXPECTED_PACKAGE_SIZE}|{EXPECTED_PACKAGE_CRC32}":
        raise LiveResourceError("current LWScripts.txt does not match the verified v14 package")
    if p["version"].read_text(encoding="utf-8-sig").strip() != str(EXPECTED_CONTENT_VERSION):
        raise LiveResourceError("current version.txt does not identify Lua content version 14")
    file_version, content_version, entries = read_lwlf(p["data"])
    if (file_version, content_version) != (EXPECTED_FILE_VERSION, EXPECTED_CONTENT_VERSION):
        raise LiveResourceError("current LWLF header no longer matches file-version 3/content-version 14")
    mapped = dict(entries)
    official = mapped.get(LUA_ENTRY)
    if official is None or hashlib.sha256(official).hexdigest() != EXPECTED_LUA_ENTRY_SHA256:
        raise LiveResourceError("current official LuaEntry no longer matches the proven loader source")
    if ORIGINAL_LUA_ENTRY in mapped:
        raise LiveResourceError("current script package already contains a preserved LuaEntry marker")
    return {
        "packageSha256": package_hash,
        "packageSize": EXPECTED_PACKAGE_SIZE,
        "packageCrc32": EXPECTED_PACKAGE_CRC32,
        "xluaSha256": EXPECTED_XLUA_SHA256,
        "assemblyCSharpSha256": EXPECTED_ASSEMBLY_CSHARP_SHA256,
        "luaEntrySha256": EXPECTED_LUA_ENTRY_SHA256,
        "entryCount": len(entries),
    }


def probe_source() -> bytes:
    return Path(__file__).with_name("current_live_resource_probe.lua").read_bytes()


def wrapper_source() -> bytes:
    prefix = b'''-- LWBRIDGE_LIVE_RESOURCE_LOADER\nlocal unpack_values = table.unpack or unpack\nlocal ok_original, original = pcall(require, "DataCenter.Global.LuaEntry_original")\nif not ok_original then error(original) end\nlocal probe = (function()\n'''
    suffix = b'''\nend)()\nif type(probe) ~= "table" then error("embedded live-resource probe did not return a table") end\npcall(probe.Pump)\nlocal function wrap(name)\n    if type(original) ~= "table" or type(original[name]) ~= "function" then return end\n    local previous = original[name]\n    original[name] = function(...)\n        local values = { pcall(previous, ...) }\n        local ok = table.remove(values, 1)\n        pcall(probe.Pump)\n        if not ok then error(values[1]) end\n        return unpack_values(values)\n    end\nend\nfor _, method in ipairs({"init", "__InitCModule", "Async_Init", "Async_Update", "AsyncUpdate", "Update", "LateUpdate"}) do wrap(method) end\nrawset(_G, "LWBridgeLiveResourceProbe", probe)\nreturn original\n'''
    return prefix + probe_source() + suffix


def make_candidate(p: dict[str, Path], directory: Path) -> dict[str, object]:
    file_version, content_version, entries = read_lwlf(p["data"])
    mapped = dict(entries)
    official = mapped[LUA_ENTRY]
    wrapper_plain = wrapper_source()
    wrapper = encode_lenc(wrapper_plain)
    if decode_lenc(wrapper) != wrapper_plain:
        raise LiveResourceError("live-resource LuaEntry LENC round-trip failed")
    mapped[LUA_ENTRY] = wrapper
    output = [(name, mapped[name]) for name, _ in entries]
    output.append((ORIGINAL_LUA_ENTRY, official))
    data = directory / "LWScripts.data"
    metadata = directory / "LWScripts.txt"
    version = directory / "version.txt"
    write_lwlf(data, file_version, content_version, output)
    verify_version, verify_content, verify_entries = read_lwlf(data)
    verify_map = dict(verify_entries)
    if (verify_version, verify_content) != (file_version, content_version):
        raise LiveResourceError("candidate LWLF header failed round-trip")
    if verify_map.get(ORIGINAL_LUA_ENTRY) != official or decode_lenc(verify_map[LUA_ENTRY]) != wrapper_plain:
        raise LiveResourceError("candidate LuaEntry preservation/serialization verification failed")
    package_crc = crc32_file(data)
    metadata.write_text(f"{data.stat().st_size}|{package_crc}", encoding="utf-8")
    version.write_text(str(content_version), encoding="utf-8")
    return {
        "packageSha256": sha256_file(data),
        "packageSize": data.stat().st_size,
        "packageCrc32": package_crc,
        "probeSourceSha256": hashlib.sha256(probe_source()).hexdigest(),
        "wrapperPlaintextSha256": hashlib.sha256(wrapper_plain).hexdigest(),
        "entryCount": len(verify_entries),
    }


def copy_atomic(source: Path, destination: Path) -> None:
    temp = destination.with_name(destination.name + ".lwbridge-live.tmp")
    shutil.copy2(source, temp)
    os.replace(temp, destination)


def make_backup(p: dict[str, Path]) -> Path:
    p["backup_root"].mkdir(parents=True, exist_ok=True)
    backup = p["backup_root"] / (time.strftime("%Y%m%d-%H%M%S", time.gmtime()) + "-" + uuid.uuid4().hex)
    backup.mkdir()
    for key in ("data", "metadata", "version"):
        shutil.copy2(p[key], backup / p[key].name)
    return backup


def restore_backup(p: dict[str, Path], backup: Path) -> dict[str, object]:
    for key in ("data", "metadata", "version"):
        copy_atomic(backup / p[key].name, p[key])
    current = verify_current(p)
    return {"restored": True, "packageSha256": current["packageSha256"]}


def install_candidate(p: dict[str, Path], candidate: Path) -> None:
    for key in ("data", "metadata", "version"):
        copy_atomic(candidate / p[key].name, p[key])


def read_json(path: Path) -> dict[str, object] | None:
    try:
        value = json.loads(path.read_text(encoding="utf-8"))
        return value if isinstance(value, dict) else None
    except (OSError, json.JSONDecodeError):
        return None


def loaded_probe_is_fresh(p: dict[str, Path]) -> bool:
    heartbeat = p["runtime"] / "heartbeat.json"
    value = read_json(heartbeat)
    if value is None or value.get("probeVersion") != PROBE_VERSION:
        return False
    try:
        age = time.time() - heartbeat.stat().st_mtime
    except OSError:
        return False
    # IMPLEMENTATION POLICY: a five-second heartbeat age is only a local liveness
    # guard for this bounded adapter; it is not original LWBridge readiness.
    return -1 <= age <= 5


def write_command(p: dict[str, Path], request_id: str) -> None:
    p["runtime"].mkdir(parents=True, exist_ok=True)
    command = p["runtime"] / "command.txt"
    temp = p["runtime"] / ("command-" + uuid.uuid4().hex + ".tmp")
    temp.write_text(f"schema=1\nrequestId={request_id}\n", encoding="utf-8")
    os.replace(temp, command)


def await_result(p: dict[str, Path], request_id: str, timeout_seconds: int) -> dict[str, object]:
    result_path = p["runtime"] / "result.json"
    deadline = time.monotonic() + timeout_seconds
    while time.monotonic() < deadline:
        value = read_json(result_path)
        if value is not None and value.get("requestId") == request_id:
            state = value.get("state")
            if state == "proven":
                return value
            if state == "failed":
                raise LiveResourceError(f"live-resource probe failed: {value.get('error')}")
        time.sleep(0.2)
    raise LiveResourceError("live-resource result did not arrive within the bounded helper timeout")


def run(request_id: str, timeout_seconds: int, restart_unmanaged: bool) -> dict[str, object]:
    p = paths()
    p["runtime"].mkdir(parents=True, exist_ok=True)

    if process_running("LastWar.exe") and loaded_probe_is_fresh(p):
        write_command(p, request_id)
        result = await_result(p, request_id, timeout_seconds)
        return {
            "ok": True,
            "mode": "reuse_loaded_probe",
            "requestId": request_id,
            "probeVersion": PROBE_VERSION,
            "resultPath": str(p["runtime"] / "result.json"),
            "result": result,
            "gameRunning": True,
            "installedFilesChanged": False,
        }

    if process_running("LastWar.exe"):
        if not restart_unmanaged:
            raise LiveResourceError("LastWar.exe is running without this bounded probe loaded")
        stop_game()

    current = verify_current(p)
    backup = make_backup(p)
    candidate_root = Path(tempfile.mkdtemp(prefix="lwbridge-live-resource-"))
    installed = False
    candidate_info: dict[str, object] | None = None
    try:
        candidate_info = make_candidate(p, candidate_root)
        install_candidate(p, candidate_root)
        installed = True
        for stale_name in ("heartbeat.json", "result.json", "command.txt"):
            try:
                (p["runtime"] / stale_name).unlink()
            except FileNotFoundError:
                pass
        write_command(p, request_id)
        subprocess.Popen([str(p["launcher"])], cwd=str(p["launcher"].parent))
        result = await_result(p, request_id, timeout_seconds)

        restore_while_running_error = None
        try:
            restored = restore_backup(p, backup)
            installed = False
        except Exception as first_restore_error:
            # The R7 live run proved that same-process disk restoration can work,
            # but Windows file sharing is an environmental condition, not a
            # recovered protocol requirement. If this run cannot replace the
            # package while LastWar has it open, close the authorized game,
            # restore exactly, and preserve the successful acquisition.
            restore_while_running_error = str(first_restore_error)
            stop_game()
            restored = restore_backup(p, backup)
            installed = False
            restored["requiredGameStop"] = True
            restored["restoreWhileRunningError"] = restore_while_running_error

        return {
            "ok": True,
            "mode": "install_launch_restore",
            "requestId": request_id,
            "probeVersion": PROBE_VERSION,
            "resultPath": str(p["runtime"] / "result.json"),
            "result": result,
            "currentClient": current,
            "candidate": candidate_info,
            "restore": restored,
            "gameRunning": process_running("LastWar.exe"),
            "installedFilesChanged": False,
        }
    except Exception:
        if installed:
            try:
                stop_game()
                restore_backup(p, backup)
                installed = False
            except Exception as restore_error:
                raise LiveResourceError(f"live-resource run failed and restore also failed: {restore_error}")
        raise
    finally:
        shutil.rmtree(candidate_root, ignore_errors=True)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--request-id")
    parser.add_argument("--timeout-seconds", type=int, default=120)
    parser.add_argument("--restart-unmanaged", action="store_true")
    parser.add_argument(
        "--check-only",
        action="store_true",
        help="verify the current fingerprint and build/round-trip a temporary candidate without changing installed files",
    )
    args = parser.parse_args()
    if args.check_only:
        candidate_root = Path(tempfile.mkdtemp(prefix="lwbridge-live-resource-check-"))
        try:
            current = verify_current(paths())
            candidate = make_candidate(paths(), candidate_root)
            print(json.dumps({"ok": True, "mode": "check_only", "currentClient": current, "candidate": candidate}))
            return 0
        except Exception as exc:
            print(json.dumps({"ok": False, "error": str(exc), "errorType": type(exc).__name__}))
            return 2
        finally:
            shutil.rmtree(candidate_root, ignore_errors=True)
    if not args.request_id or len(args.request_id) > 128 or not all(c.isalnum() or c in "_-" for c in args.request_id):
        print(json.dumps({"ok": False, "error": "invalid request id"}))
        return 2
    if args.timeout_seconds < 10 or args.timeout_seconds > 180:
        print(json.dumps({"ok": False, "error": "timeout must be 10..180 seconds"}))
        return 2
    try:
        result = run(args.request_id, args.timeout_seconds, args.restart_unmanaged)
    except Exception as exc:
        print(json.dumps({"ok": False, "error": str(exc), "errorType": type(exc).__name__}))
        return 2
    print(json.dumps(result, separators=(",", ":")))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
