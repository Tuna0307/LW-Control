#!/usr/bin/env python3
"""Hash-gated static inventory of LWBridge 0.3.17 original bridge payload assets.

No executable is launched. No runtime/auth state is read. The tool inspects only
the fixed reference EXE plus the repository/reference-artifact roots supplied to
this project.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import struct
from pathlib import Path
from typing import Any

import pefile

ROOT = Path(__file__).resolve().parents[2]
REFERENCE = Path(r"C:\Users\chimw\OneDrive\Desktop\Github\LW\lwbridge-0.3.17.exe")
EXPECTED_REFERENCE_SHA256 = "4e9c3113dedfd7e1a752404c6936aab304e67d7ffdb0952a5003c2ec948d6783"

EXPECTED_PACKAGE_RAW = 0x8EED88
EXPECTED_PACKAGE_SHA256 = "c215b5aa87619f547f2d999e58d1a1cb4780b49fec7040362732257d86d4fa82"
EXPECTED_PACKAGE_BUILD = "yoWZOvy8GWpsTNLrcZGcYQ"
EXPECTED_PACKAGE_CIPHER_LEN = 1_418_032
EXPECTED_PACKAGE_SIZE = 1_418_098

RESOURCE_NAMES_RAW = 0xA490FA
RESOURCE_NAMES = (
    "bridge-scripts.dat",
    "xlua-proxy-secure.dll",
    "xlua-proxy-plain.dll",
    "xlua-proxy-bundle.json",
    "xlua-legacy.dll",
    "lwbridge-profile-launcher.exe",
    "lwbridge-multi-hook.dll",
    "lastwar-texts",
)

SECURE_RAW = 0xA4919B
SECURE_SIZE = 619_008
SECURE_SHA256 = "ae8bba866df80e9c924923d1f59d49e824c784c8ba9b47780fe2f58305e00a86"
PLAIN_RAW = 0xAE039B
PLAIN_SIZE = 620_544
PLAIN_SHA256 = "266129c6c92f3ae89001493d61ccfdf4f5dac1fe66cff4fbb4799e6c01a6c902"
BUNDLE_RAW = 0xB77B9B
BUNDLE_SIZE = 590
BUNDLE_SHA256 = "d6b3c71208a658d3d4ff05ab1af2e5abee3f5bab05839e15d6c97f6d58cffd1c"

LEGACY_RAW = 0xB77DE9
LAUNCHER_RAW = 0xC36F79
MULTI_HOOK_RAW = 0xCDE179

MARKERS = (
    "bridge-scripts.dat",
    "package-key.envelope",
    "LWBP2|",
    "LWKE1",
    "getTreasureClaimStatus",
    "claimTreasures",
    "prepareGhostPlunderTasks",
)


class InspectError(RuntimeError):
    pass


def require(condition: bool, message: str) -> None:
    if not condition:
        raise InspectError(message)


def sha256(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def file_sha256(path: Path) -> str:
    return sha256(path.read_bytes())


def pe_file_size(data: bytes, offset: int) -> tuple[int, pefile.PE]:
    pe = pefile.PE(data=data[offset:], fast_load=False)
    section_end = max(
        [int(pe.OPTIONAL_HEADER.SizeOfHeaders)]
        + [int(s.PointerToRawData + s.SizeOfRawData) for s in pe.sections]
    )
    security = pe.OPTIONAL_HEADER.DATA_DIRECTORY[
        pefile.DIRECTORY_ENTRY["IMAGE_DIRECTORY_ENTRY_SECURITY"]
    ]
    cert_end = (
        int(security.VirtualAddress + security.Size)
        if security.VirtualAddress and security.Size
        else 0
    )
    return max(section_end, cert_end), pe


def raw_to_rva(pe: pefile.PE, raw: int) -> int:
    return int(pe.get_rva_from_offset(raw))


def marker_positions(blob: bytes, text: str, encoding: str) -> list[int]:
    needle = text.encode(encoding)
    out: list[int] = []
    start = 0
    while True:
        found = blob.find(needle, start)
        if found < 0:
            return out
        out.append(found)
        start = found + len(needle)


def parse_lwbp2(blob: bytes, offset: int) -> dict[str, Any]:
    require(blob[offset : offset + 4] == b"LWBP", "package magic changed")
    version, build_len = struct.unpack_from("<II", blob, offset + 4)
    require(version == 2, f"package version changed: {version}")
    require(build_len == 22, f"package build-id length changed: {build_len}")
    build_start = offset + 12
    build_end = build_start + build_len
    build = blob[build_start:build_end].decode("ascii")
    nonce_off = build_end
    cipher_len_off = nonce_off + 12
    cipher_len = struct.unpack_from("<I", blob, cipher_len_off)[0]
    cipher_off = cipher_len_off + 4
    tag_off = cipher_off + cipher_len
    total = tag_off + 16 - offset
    require(total == EXPECTED_PACKAGE_SIZE, f"package size changed: {total}")
    package = blob[offset : offset + total]
    return {
        "rawOffset": f"0x{offset:X}",
        "size": total,
        "sha256": sha256(package),
        "magic": "LWBP",
        "version": version,
        "buildIdLength": build_len,
        "buildId": build,
        "nonceOffsetWithinPackage": nonce_off - offset,
        "nonceHex": blob[nonce_off : nonce_off + 12].hex(),
        "ciphertextLengthOffsetWithinPackage": cipher_len_off - offset,
        "ciphertextOffsetWithinPackage": cipher_off - offset,
        "ciphertextLength": cipher_len,
        "tagOffsetWithinPackage": tag_off - offset,
        "tagHex": blob[tag_off : tag_off + 16].hex(),
        "totalRelation": "12 + buildIdLength + 12 + 4 + ciphertextLength + 16",
        "historicalLayoutShapeMatches031": True,
        "identityMatches031": False,
    }


def classify_pe(blob: bytes, offset: int, expected_name: str) -> dict[str, Any]:
    size, pe = pe_file_size(blob, offset)
    payload = blob[offset : offset + size]
    exports: list[str] = []
    if hasattr(pe, "DIRECTORY_ENTRY_EXPORT") and pe.DIRECTORY_ENTRY_EXPORT:
        exports = [
            s.name.decode("ascii", "replace")
            for s in pe.DIRECTORY_ENTRY_EXPORT.symbols
            if s.name
        ]
    imports = [
        entry.dll.decode("ascii", "replace")
        for entry in getattr(pe, "DIRECTORY_ENTRY_IMPORT", [])
    ]
    return {
        "name": expected_name,
        "rawOffset": f"0x{offset:X}",
        "size": size,
        "sha256": sha256(payload),
        "machine": f"0x{int(pe.FILE_HEADER.Machine):X}",
        "isDll": bool(int(pe.FILE_HEADER.Characteristics) & 0x2000),
        "entryPointRva": f"0x{int(pe.OPTIONAL_HEADER.AddressOfEntryPoint):X}",
        "securityDirectory": {
            "rawOffsetWithinPe": f"0x{int(pe.OPTIONAL_HEADER.DATA_DIRECTORY[4].VirtualAddress):X}",
            "size": int(pe.OPTIONAL_HEADER.DATA_DIRECTORY[4].Size),
        },
        "importDlls": imports,
        "exportCount": len(exports),
        "firstExports": exports[:16],
    }


def supplied_artifact_search() -> dict[str, Any]:
    roots = [ROOT, REFERENCE.parent]
    names = {"package-key.envelope", "bridge-scripts.dat", "authorization.ticket"}
    found: list[str] = []
    for root in roots:
        for path in root.rglob("*"):
            if path.is_file() and path.name in names:
                found.append(str(path))
    return {
        "roots": [str(p) for p in roots],
        "searchedNames": sorted(names),
        "matchingFiles": sorted(found),
        "note": "Bounded to repository and sibling supplied-reference folder; no owner runtime/config roots searched.",
    }


def inspect() -> dict[str, Any]:
    blob = REFERENCE.read_bytes()
    reference_hash = sha256(blob)
    require(reference_hash == EXPECTED_REFERENCE_SHA256, "reference EXE identity changed")

    host_pe = pefile.PE(data=blob, fast_load=False)
    last_section_end = max(
        int(s.PointerToRawData + s.SizeOfRawData) for s in host_pe.sections
    )
    require(last_section_end == len(blob), "reference unexpectedly has file overlay")

    package = parse_lwbp2(blob, EXPECTED_PACKAGE_RAW)
    require(package["sha256"] == EXPECTED_PACKAGE_SHA256, "package identity changed")
    require(package["buildId"] == EXPECTED_PACKAGE_BUILD, "package build id changed")
    require(
        package["ciphertextLength"] == EXPECTED_PACKAGE_CIPHER_LEN,
        "package ciphertext length changed",
    )
    require(
        EXPECTED_PACKAGE_RAW + EXPECTED_PACKAGE_SIZE == RESOURCE_NAMES_RAW,
        "package no longer ends at resource-name block",
    )

    names_blob = "".join(RESOURCE_NAMES).encode("ascii")
    require(
        blob[RESOURCE_NAMES_RAW : RESOURCE_NAMES_RAW + len(names_blob)] == names_blob,
        "resource-name block changed",
    )
    require(
        RESOURCE_NAMES_RAW + len(names_blob) == SECURE_RAW,
        "secure proxy no longer begins at end of resource-name block",
    )

    secure = classify_pe(blob, SECURE_RAW, "xlua-proxy-secure.dll")
    plain = classify_pe(blob, PLAIN_RAW, "xlua-proxy-plain.dll")
    require(secure["size"] == SECURE_SIZE and secure["sha256"] == SECURE_SHA256,
            "secure proxy identity changed")
    require(plain["size"] == PLAIN_SIZE and plain["sha256"] == PLAIN_SHA256,
            "plain proxy identity changed")
    require(SECURE_RAW + SECURE_SIZE == PLAIN_RAW,
            "secure/plain proxy adjacency changed")
    require(PLAIN_RAW + PLAIN_SIZE == BUNDLE_RAW,
            "plain proxy no longer ends at bundle JSON")

    bundle_raw = blob[BUNDLE_RAW : BUNDLE_RAW + BUNDLE_SIZE]
    require(sha256(bundle_raw) == BUNDLE_SHA256, "proxy bundle JSON identity changed")
    bundle = json.loads(bundle_raw.decode("utf-8"))
    require(bundle["schemaVersion"] == 1, "proxy bundle schema changed")
    require(bundle["secure"]["proxySha256"] == SECURE_SHA256,
            "bundle secure hash mismatch")
    require(bundle["plain"]["proxySha256"] == PLAIN_SHA256,
            "bundle plain hash mismatch")
    require(BUNDLE_RAW + BUNDLE_SIZE == LEGACY_RAW,
            "legacy DLL no longer begins at end of proxy bundle JSON")

    legacy = classify_pe(blob, LEGACY_RAW, "xlua-legacy.dll")
    launcher = classify_pe(blob, LAUNCHER_RAW, "lwbridge-profile-launcher.exe")
    multi_hook = classify_pe(blob, MULTI_HOOK_RAW, "lwbridge-multi-hook.dll")
    require(LEGACY_RAW + legacy["size"] == LAUNCHER_RAW,
            "signed legacy PE no longer ends at profile launcher")
    require(LAUNCHER_RAW + launcher["size"] == MULTI_HOOK_RAW,
            "profile launcher no longer ends at multi-hook")

    marker_map: dict[str, Any] = {}
    for marker in MARKERS:
        marker_map[marker] = {
            "ascii": [f"0x{x:X}" for x in marker_positions(blob, marker, "ascii")],
            "utf16le": [f"0x{x:X}" for x in marker_positions(blob, marker, "utf-16le")],
        }

    return {
        "ok": True,
        "findingId": "LWB317-FUNCTION-ORIGINAL-LUA-RECOVERY-006-A",
        "reference": {
            "path": str(REFERENCE),
            "sha256": reference_hash,
            "size": len(blob),
            "imageBase": f"0x{int(host_pe.OPTIONAL_HEADER.ImageBase):X}",
            "overlayBytes": len(blob) - last_section_end,
        },
        "bridgeScriptsPackage": {
            **package,
            "outerHostRva": f"0x{raw_to_rva(host_pe, EXPECTED_PACKAGE_RAW):X}",
            "classification": "actual embedded encrypted LWBP v2 package",
            "not": ["frontend JS", "current Last War Lua package", "controller plaintext"],
        },
        "resourceNameBlock": {
            "rawOffset": f"0x{RESOURCE_NAMES_RAW:X}",
            "outerHostRva": f"0x{raw_to_rva(host_pe, RESOURCE_NAMES_RAW):X}",
            "names": list(RESOURCE_NAMES),
            "size": len(names_blob),
        },
        "embeddedAssets": {
            "secureProxy": secure,
            "plainProxy": plain,
            "proxyBundleJson": {
                "rawOffset": f"0x{BUNDLE_RAW:X}",
                "size": BUNDLE_SIZE,
                "sha256": BUNDLE_SHA256,
                "json": bundle,
            },
            "legacyXlua": legacy,
            "profileLauncher": launcher,
            "multiHook": multi_hook,
        },
        "markers": marker_map,
        "suppliedArtifactSearch": supplied_artifact_search(),
        "classifications": {
            "outerHostBridgeScriptsMarker": (
                "ASCII resource name at package end; distinct from proxy '@bridge-scripts.dat' Lua chunk name"
            ),
            "proxyBridgeScriptsMarkers": (
                "secure/plain proxy loader chunk label '@bridge-scripts.dat'"
            ),
            "packageKeyEnvelopeUtf16Markers": (
                "secure/plain proxy runtime path suffixes"
            ),
            "packageKeyEnvelopeAsciiMarker": (
                "outer host auth/runtime-material string cluster"
            ),
            "controllerNames": (
                "outer host Lua provider call names; names alone are not controller bodies"
            ),
        },
        "disposition": {
            "payloadEmbedded": True,
            "payloadEncrypted": True,
            "usableEnvelopeFileSupplied": False,
            "standaloneBridgeScriptsFileSupplied": False,
            "currentGameLuaIsOriginalBridgePayload": False,
            "next": "recover exact 0.3.17 loader/crypto/integrity/input contract from embedded proxies; do not attempt decrypt until legitimate key inputs are complete",
        },
    }


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--output", type=Path)
    args = parser.parse_args()
    try:
        result = inspect()
    except (InspectError, OSError, pefile.PEFormatError, json.JSONDecodeError) as exc:
        parser.error(str(exc))
    encoded = json.dumps(result, indent=2) + "\n"
    if args.output:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(encoded, encoding="utf-8")
    else:
        print(encoded, end="")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
