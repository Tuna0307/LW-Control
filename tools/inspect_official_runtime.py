#!/usr/bin/env python3
"""Read-only evidence collector for the installed Last War PC client.

The inspector intentionally records architecture/version/hash/lifecycle metadata only.
It does not start the game, modify installed files, inspect user identifiers, or alter
anti-cheat components.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import re
import struct
from collections import Counter
from datetime import datetime, timezone
from pathlib import Path
from typing import Any


OFFICIAL_ROOT_LABEL = r"%LOCALAPPDATA%\FunFly\Last War-Survival Game"
LOCALLOW_ROOT_LABEL = r"%USERPROFILE%\AppData\LocalLow\FunFly\Last War-Survival Game"

PE_MACHINE_NAMES = {
    0x014C: "x86",
    0x8664: "x64",
    0xAA64: "arm64",
}


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for block in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def md5_file(path: Path) -> str:
    digest = hashlib.md5(usedforsecurity=False)
    with path.open("rb") as handle:
        for block in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def file_evidence(path: Path) -> dict[str, Any]:
    return {
        "exists": path.is_file(),
        "size": path.stat().st_size if path.is_file() else None,
        "sha256": sha256_file(path) if path.is_file() else None,
    }


def read_json(path: Path) -> Any:
    with path.open("r", encoding="utf-8-sig") as handle:
        return json.load(handle)


def read_text(path: Path) -> str:
    return path.read_text(encoding="utf-8-sig", errors="replace").strip()


def _u16(data: bytes, offset: int) -> int:
    return struct.unpack_from("<H", data, offset)[0]


def _u32(data: bytes, offset: int) -> int:
    return struct.unpack_from("<I", data, offset)[0]


def _read_c_string(data: bytes, offset: int, max_length: int = 4096) -> str:
    if offset < 0 or offset >= len(data):
        return ""
    end = data.find(b"\0", offset, min(len(data), offset + max_length))
    if end < 0:
        end = min(len(data), offset + max_length)
    return data[offset:end].decode("ascii", errors="replace")


def inspect_pe(path: Path) -> dict[str, Any]:
    result: dict[str, Any] = {
        "exists": path.is_file(),
        "size": path.stat().st_size if path.is_file() else None,
        "sha256": sha256_file(path) if path.is_file() else None,
        "isPe": False,
    }
    if not path.is_file():
        return result

    data = path.read_bytes()
    if len(data) < 0x40 or data[:2] != b"MZ":
        return result

    pe_offset = _u32(data, 0x3C)
    if pe_offset + 24 > len(data) or data[pe_offset : pe_offset + 4] != b"PE\0\0":
        return result

    coff = pe_offset + 4
    machine = _u16(data, coff)
    section_count = _u16(data, coff + 2)
    optional_size = _u16(data, coff + 16)
    optional = coff + 20
    if optional + optional_size > len(data):
        return result

    magic = _u16(data, optional)
    if magic == 0x20B:
        bits = 64
        directory_base = optional + 112
    elif magic == 0x10B:
        bits = 32
        directory_base = optional + 96
    else:
        bits = None
        directory_base = None

    sections: list[tuple[int, int, int, int]] = []
    section_base = optional + optional_size
    for index in range(section_count):
        offset = section_base + index * 40
        if offset + 40 > len(data):
            break
        virtual_size = _u32(data, offset + 8)
        virtual_address = _u32(data, offset + 12)
        raw_size = _u32(data, offset + 16)
        raw_pointer = _u32(data, offset + 20)
        sections.append((virtual_address, max(virtual_size, raw_size), raw_pointer, raw_size))

    def rva_to_offset(rva: int) -> int | None:
        if rva == 0:
            return None
        for virtual_address, span, raw_pointer, raw_size in sections:
            if virtual_address <= rva < virtual_address + span:
                delta = rva - virtual_address
                if delta >= raw_size:
                    return None
                offset = raw_pointer + delta
                return offset if 0 <= offset < len(data) else None
        return rva if 0 <= rva < len(data) else None

    imports: list[str] = []
    exports: list[str] = []
    export_function_count = 0
    if directory_base is not None and directory_base + 16 <= optional + optional_size:
        export_rva, export_size = struct.unpack_from("<II", data, directory_base)
        import_rva, import_size = struct.unpack_from("<II", data, directory_base + 8)

        import_offset = rva_to_offset(import_rva)
        if import_offset is not None and import_size:
            for descriptor_index in range(4096):
                descriptor = import_offset + descriptor_index * 20
                if descriptor + 20 > len(data):
                    break
                fields = struct.unpack_from("<IIIII", data, descriptor)
                if fields == (0, 0, 0, 0, 0):
                    break
                name_offset = rva_to_offset(fields[3])
                if name_offset is not None:
                    name = _read_c_string(data, name_offset)
                    if name:
                        imports.append(name)

        export_offset = rva_to_offset(export_rva)
        if export_offset is not None and export_size and export_offset + 40 <= len(data):
            export_function_count = _u32(data, export_offset + 20)
            number_of_names = _u32(data, export_offset + 24)
            address_of_names = _u32(data, export_offset + 32)
            names_offset = rva_to_offset(address_of_names)
            if names_offset is not None:
                for name_index in range(min(number_of_names, 65536)):
                    item_offset = names_offset + name_index * 4
                    if item_offset + 4 > len(data):
                        break
                    name_rva = _u32(data, item_offset)
                    name_offset = rva_to_offset(name_rva)
                    if name_offset is not None:
                        name = _read_c_string(data, name_offset)
                        if name:
                            exports.append(name)

    notable_prefixes = (
        "il2cpp_",
        "lua_",
        "luaL_",
        "xlua_",
        "InitMono",
        "LoadAssembly",
        "SetAssembly",
        "NvOptimus",
        "AmdPower",
    )
    notable_exports = [
        name for name in exports if any(name.startswith(prefix) for prefix in notable_prefixes)
    ][:80]

    result.update(
        {
            "isPe": True,
            "machine": f"0x{machine:04x}",
            "machineName": PE_MACHINE_NAMES.get(machine, "unknown"),
            "bits": bits,
            "sectionCount": section_count,
            "imports": sorted(set(imports), key=str.lower),
            "exportFunctionCount": export_function_count,
            "namedExportCount": len(exports),
            "notableExports": notable_exports,
        }
    )
    return result


def manifest_inventory(manifest: dict[str, Any]) -> dict[str, Any]:
    files = manifest.get("files") if isinstance(manifest.get("files"), list) else []
    extension_counts: Counter[str] = Counter()
    top_level_counts: Counter[str] = Counter()
    anti_cheat_entries = 0
    for entry in files:
        if not isinstance(entry, dict):
            continue
        raw_path = str(entry.get("path", ""))
        normalized = raw_path.replace("/", "\\")
        suffix = Path(normalized).suffix.lower() or "<none>"
        extension_counts[suffix] += 1
        top_level = normalized.split("\\", 1)[0] if normalized else "<none>"
        top_level_counts[top_level] += 1
        if normalized.lower().startswith("anticheatexpert\\"):
            anti_cheat_entries += 1
    return {
        "fileCount": len(files),
        "extensionCounts": dict(sorted(extension_counts.items())),
        "topLevelCounts": dict(sorted(top_level_counts.items())),
        "antiCheatEntryCount": anti_cheat_entries,
    }


def inspect_container(path: Path) -> dict[str, Any]:
    result = file_evidence(path)
    if not path.is_file():
        return result
    prefix = path.read_bytes()[:16]
    result.update(
        {
            "prefixHex": prefix.hex(),
            "startsMZ": prefix.startswith(b"MZ"),
            "startsRG": prefix.startswith(b"RG"),
        }
    )
    return result


def inspect_hotupdate(local_low_root: Path) -> dict[str, Any]:
    scripts = local_low_root / "lwScripts"
    table = local_low_root / "table"
    data_path = scripts / "LWScripts.data"
    metadata_path = scripts / "LWScripts.txt"
    version_path = scripts / "version.txt"

    result: dict[str, Any] = {
        "root": LOCALLOW_ROOT_LABEL,
        "lwScripts": {
            "data": file_evidence(data_path),
            "metadata": file_evidence(metadata_path),
            "version": file_evidence(version_path),
        },
        "latestTable": None,
    }

    if metadata_path.is_file():
        metadata = read_text(metadata_path)
        result["lwScripts"]["metadataValue"] = metadata if re.fullmatch(r"\d+\|\d+", metadata) else "[REDACTED]"
    if version_path.is_file():
        version = read_text(version_path)
        result["lwScripts"]["versionValue"] = version if re.fullmatch(r"\d+", version) else "[REDACTED]"

    if table.is_dir():
        table_files = sorted(
            (path for path in table.glob("*.data") if path.is_file()),
            key=lambda path: path.stat().st_mtime,
            reverse=True,
        )
        if table_files:
            latest = table_files[0]
            result["latestTable"] = {
                "name": latest.name,
                **file_evidence(latest),
            }
    return result


def inspect_launcher_log(path: Path) -> dict[str, Any]:
    result: dict[str, Any] = {
        "exists": path.is_file(),
        "gameStartCount": 0,
        "orderedPreflightCount": 0,
        "preparedRelaunchCount": 0,
        "latestLocalManifestVersion": None,
        "latestRemoteManifestVersion": None,
        "latestLuaState": None,
        "latestPreparedRelaunch": None,
    }
    if not path.is_file():
        return result

    lines = path.read_text(encoding="utf-8", errors="replace").splitlines()
    local_manifest_pattern = re.compile(r"Local manifest version: (\d+)")
    remote_manifest_pattern = re.compile(r"Remote manifest version: [^,]*,(\d+),")
    lua_pattern = re.compile(
        r"LWLuaFile is current\. local=LwLuaState \{ version: (\d+), file_version: (\d+), size: (\d+), crc: (\d+), exists: (true|false) \}, remote_version=(\d+), format=([A-Za-z0-9_]+)"
    )
    relaunch_pattern = re.compile(
        r"Prepared game relaunch: pid=\d+, outcome=([A-Za-z0-9_]+), elapsed_ms=(\d+)"
    )

    marker_names = (
        "local_manifest",
        "remote_manifest",
        "data_table",
        "lua_current",
        "pack_start",
        "pack_verified",
    )
    markers: dict[str, int | None] = {name: None for name in marker_names}

    for index, line in enumerate(lines):
        if match := local_manifest_pattern.search(line):
            markers["local_manifest"] = index
            result["latestLocalManifestVersion"] = int(match.group(1))
        if match := remote_manifest_pattern.search(line):
            markers["remote_manifest"] = index
            result["latestRemoteManifestVersion"] = int(match.group(1))
        if "Data table is current." in line or "Applied data table update:" in line:
            markers["data_table"] = index
        if match := lua_pattern.search(line):
            markers["lua_current"] = index
            result["latestLuaState"] = {
                "version": int(match.group(1)),
                "fileVersion": int(match.group(2)),
                "size": int(match.group(3)),
                "crc": int(match.group(4)),
                "exists": match.group(5) == "true",
                "remoteVersion": int(match.group(6)),
                "format": match.group(7),
            }
        if "Starting pack sync with pack directory:" in line:
            markers["pack_start"] = index
        if "pack: index hash verification succeeded" in line:
            markers["pack_verified"] = index
        if "Starting game at:" in line and "LastWar.exe" in line:
            result["gameStartCount"] += 1
            positions = [markers[name] for name in marker_names]
            if all(position is not None for position in positions):
                ordered = [int(position) for position in positions if position is not None]
                if ordered == sorted(ordered) and ordered[-1] < index:
                    result["orderedPreflightCount"] += 1
            markers = {name: None for name in marker_names}
        if match := relaunch_pattern.search(line):
            result["preparedRelaunchCount"] += 1
            result["latestPreparedRelaunch"] = {
                "outcome": match.group(1),
                "elapsedMs": int(match.group(2)),
            }

    return result


def build_report(root: Path, local_low_root: Path) -> dict[str, Any]:
    manifest_path = root / "manifest.json"
    launcher_config_path = root / "LastWarLauncher.json"
    app_version_path = root / "Game" / "AppVersion.info"
    app_manifest_path = root / "Game" / "LastWar_Data" / "AppManifest.json"
    streaming_version_path = root / "Game" / "LastWar_Data" / "StreamingAssets" / "VERSION.txt"

    manifest = read_json(manifest_path) if manifest_path.is_file() else {}
    launcher_config = read_json(launcher_config_path) if launcher_config_path.is_file() else {}
    app_manifest = read_json(app_manifest_path) if app_manifest_path.is_file() else {}
    app_version = read_text(app_version_path) if app_version_path.is_file() else ""
    streaming_version = read_text(streaming_version_path) if streaming_version_path.is_file() else ""

    pe_paths = {
        "LastWarLauncher.exe": root / "LastWarLauncher.exe",
        "LastWarSync.exe": root / "LastWarSync.exe",
        "Game/LastWar.exe": root / "Game" / "LastWar.exe",
        "Game/GameAssembly.dll": root / "Game" / "GameAssembly.dll",
        "Game/LastWarBase.dll": root / "Game" / "LastWarBase.dll",
        "Game/LastWar_Data/Plugins/x86_64/xlua.dll": root
        / "Game"
        / "LastWar_Data"
        / "Plugins"
        / "x86_64"
        / "xlua.dll",
    }

    container_paths = {
        "Assembly-CSharp.rdl": root / "Game" / "LastWar_Data" / "Assemblies" / "Assembly-CSharp.rdl",
        "BaseUtils.rdl": root / "Game" / "LastWar_Data" / "Assemblies" / "BaseUtils.rdl",
        "SmartFox2X.rdl": root / "Game" / "LastWar_Data" / "Assemblies" / "SmartFox2X.rdl",
        "XLuaRuntime.rdl": root / "Game" / "LastWar_Data" / "Assemblies" / "XLuaRuntime.rdl",
        "UnityEngine.CoreModule.mdl": root
        / "Game"
        / "LastWar_Data"
        / "Assemblies"
        / "UnityEngine.CoreModule.mdl",
        "mscorlib.mdl": root / "Game" / "LastWar_Data" / "Assemblies" / "mscorlib.mdl",
    }

    protected_runtime_paths = {
        "ACE-Base64.dll": root / "Game" / "AntiCheatExpert" / "ACE-Base64.dll",
        "ACE-Service64.exe": root / "Game" / "AntiCheatExpert" / "ACE-Service64.exe",
    }

    report = {
        "schema": 1,
        "observedAtUtc": datetime.now(timezone.utc).isoformat(),
        "mode": "read-only",
        "officialRoot": OFFICIAL_ROOT_LABEL,
        "versions": {
            "manifestAppVersion": manifest.get("app_version"),
            "manifestAppVersionCode": manifest.get("app_version_code"),
            "manifestLauncherVersion": manifest.get("launcher_version"),
            "appVersionInfo": app_version,
            "appManifestVersion": app_manifest.get("version") if isinstance(app_manifest, dict) else None,
            "appManifestBuildTarget": app_manifest.get("buildTarget") if isinstance(app_manifest, dict) else None,
            "streamingAssetsVersion": streaming_version,
            "launcherDisplayVersion": launcher_config.get("display_version")
            if isinstance(launcher_config, dict)
            else None,
        },
        "manifest": {
            "packageName": manifest.get("package_name"),
            "platform": manifest.get("platform"),
            "channel": manifest.get("channel"),
            "launcherMetadata": manifest.get("launcher"),
            "inventory": manifest_inventory(manifest if isinstance(manifest, dict) else {}),
        },
        "pe": {name: inspect_pe(path) for name, path in pe_paths.items()},
        "containerSamples": {name: inspect_container(path) for name, path in container_paths.items()},
        "protectedRuntimeHashes": {
            name: file_evidence(path) for name, path in protected_runtime_paths.items()
        },
        "hotupdate": inspect_hotupdate(local_low_root),
        "launcherLog": inspect_launcher_log(root / "Launcher.log"),
        "integrityChecks": {},
    }


    launcher_metadata = manifest.get("launcher") if isinstance(manifest, dict) else None
    installed_launcher = root / "LastWarLauncher.exe"
    metadata_launcher_path = (
        root / str(launcher_metadata.get("path"))
        if isinstance(launcher_metadata, dict) and launcher_metadata.get("path")
        else None
    )
    report["manifest"]["launcherInstalledComparison"] = {
        "metadataPathExists": metadata_launcher_path.is_file() if metadata_launcher_path else False,
        "installedName": "LastWarLauncher.exe",
        "installedSize": installed_launcher.stat().st_size if installed_launcher.is_file() else None,
        "metadataSizeMatchesInstalled": bool(
            installed_launcher.is_file()
            and isinstance(launcher_metadata, dict)
            and launcher_metadata.get("size") == installed_launcher.stat().st_size
        ),
        "metadataHashMatchesInstalledMd5": bool(
            installed_launcher.is_file()
            and isinstance(launcher_metadata, dict)
            and isinstance(launcher_metadata.get("hash"), str)
            and launcher_metadata.get("hash", "").lower() == md5_file(installed_launcher).lower()
        ),
    }

    version_parts = app_version.split("|", 1) if app_version else []
    report["integrityChecks"] = {
        "appVersionMatchesManifest": len(version_parts) == 2
        and version_parts[0] == str(manifest.get("app_version"))
        and version_parts[1] == str(manifest.get("app_version_code")),
        "appManifestVersionMatches": str(app_manifest.get("version")) == str(manifest.get("app_version"))
        if isinstance(app_manifest, dict)
        else False,
        "standaloneWindows64": app_manifest.get("buildTarget") == "StandaloneWindows64"
        if isinstance(app_manifest, dict)
        else False,
        "requiredPeFilesPresent": all(item["exists"] and item["isPe"] for item in report["pe"].values()),
    }
    return report


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--root",
        type=Path,
        default=Path(os.environ.get("LOCALAPPDATA", "")) / "FunFly" / "Last War-Survival Game",
        help="Official Last War install root.",
    )
    parser.add_argument(
        "--local-low-root",
        type=Path,
        default=Path(os.environ.get("USERPROFILE", ""))
        / "AppData"
        / "LocalLow"
        / "FunFly"
        / "Last War-Survival Game",
        help="Per-user Last War runtime/hot-update root.",
    )
    parser.add_argument("--output", type=Path, help="Optional JSON output path.")
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    report = build_report(args.root, args.local_low_root)
    serialized = json.dumps(report, indent=2, ensure_ascii=False) + "\n"
    if args.output:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(serialized, encoding="utf-8")
    print(serialized, end="")
    checks = report["integrityChecks"]
    return 0 if all(checks.values()) else 1


if __name__ == "__main__":
    raise SystemExit(main())
