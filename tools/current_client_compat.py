"""Fail-closed current-client compatibility gate for LWBridge helpers.

IMPLEMENTATION POLICY: package identity may advance automatically only when the
recovered runtime and critical Lua anchors remain exact. Core/critical changes
remain unsupported until separately recovered and validated.
"""
from __future__ import annotations

import hashlib
from pathlib import Path

POLICY = "lwbridge-current-client-critical-anchors-1"
EXPECTED_FILE_VERSION = 3
EXPECTED_GAME_SHA256 = "df5abcf8618d48500befa9f587b509ed4f58373ff34932bb87ce217f0cf267d5"
EXPECTED_XLUA_SHA256 = "21eb704afdb7e528f4b90fa1b90bf414c221b06ba990d625aaaaed31b292740f"
EXPECTED_ASSEMBLY_SHA256 = "871efe06819fbac438413eb96b7df8193d0be56094f3a44d5ff141e6219adcbd"
CRITICAL_ENTRIES = {
    "DataCenter/Global/LuaEntry.luac": "50f3ae906a8e9898549c4ea740eedc772a88eb2979e165eb35733192d100a137",
    "Global/ConstDefine.luac": "95e6c733b98dc641c330f36efdef044845033b37ea6703068f7c7ed5fb0048fd",
    "Util/CSharpCallLuaInterface.luac": "af1559723afba0fa5773bb815c486f3233fecb3928768abd082a96b51960229e",
    "UI/LWMainUI/Component/UIMainBottom/UIMainChangeScene.luac": "3843dc02869330f060a199b946ef3cd30aa335504914c2f4c60f8a824e871c5b",
}


class CurrentClientCompatibilityError(RuntimeError):
    pass
def _sha256_bytes(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def inspect_current(lr, p: dict[str, Path]) -> dict[str, object]:
    problems: list[str] = []
    for key in ("launcher", "game", "xlua", "assembly", "data", "metadata", "version"):
        if not p[key].is_file():
            problems.append(f"missing required current-client file: {p[key]}")
    if problems:
        return {"ok": False, "compatibilityPolicy": POLICY, "problems": problems}

    game_hash = lr.sha256_file(p["game"])
    xlua_hash = lr.sha256_file(p["xlua"])
    assembly_hash = lr.sha256_file(p["assembly"])
    if game_hash != EXPECTED_GAME_SHA256:
        problems.append("LastWar.exe changed from the recovered supported build")
    if xlua_hash != EXPECTED_XLUA_SHA256:
        problems.append("xlua.dll changed from the recovered LENC contract")
    if assembly_hash != EXPECTED_ASSEMBLY_SHA256:
        problems.append("Assembly-CSharp.rdl changed from the recovered map/runtime contract")

    file_version, content_version, entries = lr.read_lwlf(p["data"])
    mapped = dict(entries)
    if file_version != EXPECTED_FILE_VERSION:
        problems.append(f"LWLF file version changed from {EXPECTED_FILE_VERSION} to {file_version}")
    if content_version <= 0:
        problems.append("LWLF content version is not positive")
    if lr.ORIGINAL_LUA_ENTRY in mapped:
        problems.append("script package already contains the LWBridge preserved-entry marker")

    critical_hashes: dict[str, str | None] = {}
    for name, expected in CRITICAL_ENTRIES.items():
        value = mapped.get(name)
        actual = _sha256_bytes(value) if value is not None else None
        critical_hashes[name] = actual
        if actual != expected:
            problems.append(f"critical Lua entry changed: {name}")

    package_size = p["data"].stat().st_size
    package_crc = lr.crc32_file(p["data"])
    package_hash = lr.sha256_file(p["data"])
    metadata = p["metadata"].read_text(encoding="utf-8-sig").strip()
    version_text = p["version"].read_text(encoding="utf-8-sig").strip()
    if metadata != f"{package_size}|{package_crc}":
        problems.append("LWScripts.txt does not match the current package size/CRC")
    if version_text != str(content_version):
        problems.append("version.txt does not match the current LWLF content version")

    observed = {
        "packageSha256": package_hash,
        "packageSize": package_size,
        "packageCrc32": package_crc,
        "fileVersion": file_version,
        "contentVersion": content_version,
        "entryCount": len(entries),
        "gameSha256": game_hash,
        "xluaSha256": xlua_hash,
        "assemblyCSharpSha256": assembly_hash,
        "luaEntrySha256": critical_hashes["DataCenter/Global/LuaEntry.luac"],
        "criticalEntries": critical_hashes,
        "metadata": metadata,
        "version": version_text,
    }
    return {
        "ok": not problems,
        "compatibilityPolicy": POLICY,
        "observed": observed,
        "problems": problems,
    }


def install_dynamic_verifier(lr) -> None:
    if getattr(lr, "_lwbridge_dynamic_verifier_installed", False):
        return
    original_verify = lr.verify_current

    def verify_current(p: dict[str, Path]) -> dict[str, object]:
        report = inspect_current(lr, p)
        if not report["ok"]:
            details = "; ".join(report.get("problems", []))
            raise CurrentClientCompatibilityError(
                "current Last War update is not automatically compatible: " + details
            )
        observed = dict(report["observed"])
        lr.EXPECTED_CONTENT_VERSION = int(observed["contentVersion"])
        lr.EXPECTED_PACKAGE_SHA256 = str(observed["packageSha256"])
        lr.EXPECTED_PACKAGE_SIZE = int(observed["packageSize"])
        lr.EXPECTED_PACKAGE_CRC32 = int(observed["packageCrc32"])
        try:
            verified = dict(original_verify(p))
        except Exception as exc:
            raise CurrentClientCompatibilityError(
                "current Last War files changed during compatibility verification: " + str(exc)
            ) from exc
        verified.update(observed)
        verified["compatibilityPolicy"] = POLICY
        return verified

    lr.verify_current = verify_current
    lr._lwbridge_dynamic_verifier_installed = True
