"""Install, inspect, or uninstall the verified persistent Daily Task runtime."""

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
    from .extract_lenc_v3 import decode_lenc_bytes, derive_xlua_key_nonce
    from .install_loader_probe import (
        InstallRefused,
        LUA_ENTRY,
        ORIGINAL_LUA_ENTRY,
        _entry_map,
        _make_backup,
        _restore_from_backup,
        _xlua_path,
        discover_paths,
        game_is_running,
        read_lwlf,
        sha256_file,
    )
    from .prepare_daily_task_runtime import RUNTIME_ENTRY, _runtime_source, _wrapper_source, verify
except ImportError:
    from extract_lenc_v3 import decode_lenc_bytes, derive_xlua_key_nonce
    from install_loader_probe import (
        InstallRefused,
        LUA_ENTRY,
        ORIGINAL_LUA_ENTRY,
        _entry_map,
        _make_backup,
        _restore_from_backup,
        _xlua_path,
        discover_paths,
        game_is_running,
        read_lwlf,
        sha256_file,
    )
    from prepare_daily_task_runtime import RUNTIME_ENTRY, _runtime_source, _wrapper_source, verify


MANIFEST_NAME = "daily-task-runtime-install.json"


def _hashes(paths: dict[str, Path]) -> dict[str, str]:
    return {key: sha256_file(paths[key]) for key in ("data", "metadata", "version", "baseutils")}


def _manifest_path(paths: dict[str, Path]) -> Path:
    return paths["runtime"] / MANIFEST_NAME


def _read_manifest(paths: dict[str, Path]) -> dict | None:
    path = _manifest_path(paths)
    if not path.is_file():
        return None
    try:
        value = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, UnicodeDecodeError, json.JSONDecodeError) as exc:
        raise InstallRefused(f"Daily Task runtime install manifest is unreadable: {exc}") from exc
    if not isinstance(value, dict) or value.get("schemaVersion") != 1:
        raise InstallRefused("Daily Task runtime install manifest schema is invalid")
    return value


def inspect(paths: dict[str, Path]) -> dict[str, object]:
    file_version, content_version, entries = read_lwlf(paths["data"])
    mapped = _entry_map(entries)
    runtime_present = RUNTIME_ENTRY in mapped
    original_present = ORIGINAL_LUA_ENTRY in mapped
    payloads_match = False
    payload_error = None
    if runtime_present and original_present:
        try:
            native = derive_xlua_key_nonce(_xlua_path(paths))
            wrapper = decode_lenc_bytes(mapped[LUA_ENTRY], native["key"], native["nonce"])["decoded"]
            runtime = decode_lenc_bytes(mapped[RUNTIME_ENTRY], native["key"], native["nonce"])["decoded"]
            payloads_match = wrapper == _wrapper_source() and runtime == _runtime_source()
        except Exception as exc:  # inspection reports, never mutates
            payload_error = str(exc)
    manifest = _read_manifest(paths)
    current_hashes = _hashes(paths)
    manifest_hash_match = False
    if manifest is not None and isinstance(manifest.get("installedHashes"), dict):
        manifest_hash_match = all(
            current_hashes.get(key) == manifest["installedHashes"].get(key)
            for key in ("data", "metadata", "version", "baseutils")
        )
    return {
        "file_version": file_version,
        "content_version": content_version,
        "entry_count": len(entries),
        "runtime_entry_present": runtime_present,
        "preserved_original_present": original_present,
        "payloads_match_current_source": payloads_match,
        "payload_error": payload_error,
        "manifest_present": manifest is not None,
        "manifest_hash_match": manifest_hash_match,
        "installed": runtime_present and original_present and payloads_match and manifest is not None and manifest_hash_match,
        "current_hashes": current_hashes,
        "manifest": manifest,
    }


def _atomic_copy(source: Path, destination: Path) -> None:
    descriptor, temporary_name = tempfile.mkstemp(
        prefix=f".{destination.name}.daily-runtime-", suffix=".tmp", dir=destination.parent
    )
    os.close(descriptor)
    temporary = Path(temporary_name)
    try:
        shutil.copy2(source, temporary)
        os.replace(temporary, destination)
    finally:
        temporary.unlink(missing_ok=True)


def install(paths: dict[str, Path], candidate_dir: Path) -> dict[str, object]:
    if game_is_running():
        raise InstallRefused("LastWar.exe is running; close it before installing the Daily Task runtime")
    current = inspect(paths)
    if current["installed"]:
        return {"changed": False, "reason": "already_installed", "inspection": current}
    if current["runtime_entry_present"] or current["preserved_original_present"] or current["manifest_present"]:
        raise InstallRefused("A partial or different Daily Task runtime installation is present; inspect before changing it")

    verified = verify(paths, candidate_dir)
    candidate_dir = Path(str(verified["candidate_dir"]))
    candidate_files = {
        "data": candidate_dir / "LWScripts.data",
        "metadata": candidate_dir / "LWScripts.txt",
        "version": candidate_dir / "version.txt",
    }
    original_hashes = _hashes(paths)
    backup = _make_backup(paths)
    try:
        for key in ("data", "metadata", "version"):
            _atomic_copy(candidate_files[key], paths[key])
        installed_hashes = _hashes(paths)
        expected = {key: sha256_file(candidate_files[key]) for key in ("data", "metadata", "version")}
        if any(installed_hashes[key] != expected[key] for key in expected):
            raise InstallRefused("Installed Daily Task runtime files do not match verified candidate hashes")
        if installed_hashes["baseutils"] != original_hashes["baseutils"]:
            raise InstallRefused("BaseUtils.rdl changed unexpectedly during Daily Task runtime installation")

        paths["runtime"].mkdir(parents=True, exist_ok=True)
        manifest = {
            "schemaVersion": 1,
            "runtimeVersion": "lwcontrol-daily-task-runtime-1",
            "installedAt": datetime.now(timezone.utc).isoformat().replace("+00:00", "Z"),
            "contentVersion": verified["content_version"],
            "candidatePackageSha256": verified["package_sha256"],
            "backupDirectory": str(backup),
            "originalHashes": original_hashes,
            "installedHashes": installed_hashes,
        }
        manifest_path = _manifest_path(paths)
        temporary_manifest = manifest_path.with_name(f".{manifest_path.name}.{os.getpid()}.tmp")
        temporary_manifest.write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
        os.replace(temporary_manifest, manifest_path)
        after = inspect(paths)
        if not after["installed"]:
            raise InstallRefused("Post-install Daily Task runtime inspection did not validate the installation")
        return {
            "changed": True,
            "candidate_dir": str(candidate_dir),
            "backup": str(backup),
            "manifest": str(manifest_path),
            "inspection": after,
        }
    except Exception:
        _restore_from_backup(paths, backup)
        _manifest_path(paths).unlink(missing_ok=True)
        raise


def uninstall(paths: dict[str, Path]) -> dict[str, object]:
    if game_is_running():
        raise InstallRefused("LastWar.exe is running; close it before uninstalling the Daily Task runtime")
    current = inspect(paths)
    manifest = current.get("manifest")
    if not isinstance(manifest, dict):
        raise InstallRefused("Daily Task runtime install manifest is missing; refusing blind restore")
    if not current["manifest_hash_match"]:
        raise InstallRefused("Installed game files changed since Daily Task runtime installation; refusing stale-backup restore")
    backup = Path(str(manifest.get("backupDirectory") or ""))
    original_hashes = manifest.get("originalHashes")
    if not isinstance(original_hashes, dict):
        raise InstallRefused("Daily Task runtime install manifest has no original hash set")
    _restore_from_backup(paths, backup)
    restored = _hashes(paths)
    if any(restored.get(key) != original_hashes.get(key) for key in ("data", "metadata", "version", "baseutils")):
        raise InstallRefused("Daily Task runtime uninstall did not restore the recorded original hashes")
    _manifest_path(paths).unlink(missing_ok=True)
    return {"changed": True, "restored_from": str(backup), "restored_hashes": restored}


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    group = parser.add_mutually_exclusive_group(required=True)
    group.add_argument("--status", action="store_true")
    group.add_argument("--install", type=Path, metavar="CANDIDATE_DIR")
    group.add_argument("--uninstall", action="store_true")
    parser.add_argument("--json", action="store_true")
    args = parser.parse_args()
    try:
        paths = discover_paths()
        if args.status:
            result = inspect(paths)
        elif args.install:
            result = install(paths, args.install)
        else:
            result = uninstall(paths)
    except (InstallRefused, OSError, ValueError, KeyError) as exc:
        payload = {"ok": False, "error": str(exc), "error_type": type(exc).__name__}
        print(json.dumps(payload, indent=2) if args.json else f"REFUSED: {exc}")
        return 2
    payload = {"ok": True, **result}
    print(json.dumps(payload, indent=2) if args.json else payload)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
