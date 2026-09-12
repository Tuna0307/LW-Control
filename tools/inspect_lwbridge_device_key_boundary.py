#!/usr/bin/env python3
"""Verify the LWBridge device-key/package-envelope prerequisite boundary.

This build-specific inspector is read-only. It hash-gates lwbridge-0.3.1.exe,
checks the host auth-service string cluster, verifies matching device-key and
package-envelope vocabulary in both embedded xLua proxies, and inventories the
Windows key/crypto imports present in the host and proxies. It does not read
authorization files, open the persisted device key, decrypt bridge-scripts.dat,
or execute LWBridge/Last War.
"""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
from typing import Any

import pefile


EXPECTED_SHA256 = "2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff"

EMBEDDED_PROXIES = {
    "xlua-proxy-secure.dll": {
        "offset": 0x987374,
        "size": 612_352,
        "sha256": "481c636b8d0fa9bf4b8f88cba77145054b33e3701ffa42737567b211859ac400",
    },
    "xlua-proxy-plain.dll": {
        "offset": 0xA1CB74,
        "size": 614_400,
        "sha256": "c9a88ec449cc6a9929fb2f2800ebd443f9b9dcdac849e3d3e8da94281dea9794",
    },
}

HOST_AUTH_MARKERS = (
    r"src\services\auth.rs",
    r"HKLM\SOFTWARE\Microsoft\Cryptography/vMachineGuid",
    "DEVICE_FINGERPRINT_UNAVAILABLE",
    "DEVICE_KEY_CLEANUP_PENDING",
    "DEVICE_KEY_MISSING",
    "DEVICE_KEY_MISMATCH",
    "DEVICE_KEY_UNAVAILABLE",
    "KEY_ENVELOPE_EXPIRED",
    "packageKeyEnvelope",
    "packageKeyEnvelopeExpiresAt",
    "KEY_ENVELOPE_INVALID",
    "LWKE1",
    "device-key-cleanup.pending",
    "authorization.challenge",
    "authorization.ticket",
    "package-key.envelope",
    "build.manifest",
)

HOST_REQUIRED_IMPORTS = {
    "ncrypt.dll": {
        "NCryptOpenStorageProvider",
        "NCryptCreatePersistedKey",
        "NCryptFinalizeKey",
        "NCryptExportKey",
        "NCryptDeleteKey",
        "NCryptOpenKey",
    },
    "crypt32.dll": {
        "CryptProtectData",
        "CryptUnprotectData",
    },
}

PROXY_ASCII_MARKERS = (
    "LWKE1",
    "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_",
    "device key missing",
    "device key unavailable",
    "device key mismatch",
    "key envelope agreement failed",
    "key envelope decrypt failed",
    "package decrypt failed",
    "package integrity invalid",
    "package build mismatch",
    "LWBP2|",
)

PROXY_UTF16_MARKERS = (
    "LWBRIDGE_PROFILE_RUNTIME_ROOT",
    r"\bridge-runtime\package-key.envelope",
    r"\bridge-runtime\build.manifest",
    r"\LastWar-xLua-Bridge\bridge-scripts.dat",
)

PROXY_REQUIRED_IMPORTS = {
    "ncrypt.dll": {
        "NCryptOpenStorageProvider",
        "NCryptOpenKey",
        "NCryptImportKey",
        "NCryptSecretAgreement",
        "NCryptDeriveKey",
        "NCryptExportKey",
    },
    "bcrypt.dll": {
        "BCryptOpenAlgorithmProvider",
        "BCryptImportKeyPair",
        "BCryptGenerateSymmetricKey",
        "BCryptDecrypt",
        "BCryptCreateHash",
        "BCryptHashData",
        "BCryptFinishHash",
        "BCryptVerifySignature",
    },
    "kernel32.dll": {
        "GetEnvironmentVariableW",
        "CreateFileW",
        "ReadFile",
        "GetFileSizeEx",
    },
}


class InspectError(ValueError):
    pass


def _sha256(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def _find_once(data: bytes, marker: bytes, label: str) -> int:
    first = data.find(marker)
    if first < 0:
        raise InspectError(f"missing marker {label!r}")
    second = data.find(marker, first + 1)
    if second >= 0:
        raise InspectError(f"marker {label!r} is not unique: 0x{first:X}, 0x{second:X}")
    return first


def _find_all(data: bytes, marker: bytes) -> list[int]:
    rows: list[int] = []
    cursor = 0
    while True:
        hit = data.find(marker, cursor)
        if hit < 0:
            return rows
        rows.append(hit)
        cursor = hit + 1


def _imports(pe: pefile.PE) -> dict[str, set[str]]:
    pe.parse_data_directories(
        directories=[pefile.DIRECTORY_ENTRY["IMAGE_DIRECTORY_ENTRY_IMPORT"]]
    )
    result: dict[str, set[str]] = {}
    for entry in getattr(pe, "DIRECTORY_ENTRY_IMPORT", []):
        dll = entry.dll.decode("ascii", "replace").lower()
        names = result.setdefault(dll, set())
        for item in entry.imports:
            if item.name is not None:
                names.add(item.name.decode("ascii", "replace"))
    return result


def _verify_imports(
    present: dict[str, set[str]], required: dict[str, set[str]], scope: str
) -> dict[str, list[str]]:
    rows: dict[str, list[str]] = {}
    for dll, names in required.items():
        actual = present.get(dll, set())
        missing = sorted(names - actual)
        if missing:
            raise InspectError(f"{scope} missing {dll} imports: {', '.join(missing)}")
        rows[dll] = sorted(names)
    return rows


def inspect(path: Path) -> dict[str, Any]:
    data = path.read_bytes()
    digest = _sha256(data)
    if digest != EXPECTED_SHA256:
        raise InspectError(
            f"unsupported lwbridge SHA-256 {digest}; expected {EXPECTED_SHA256}"
        )

    host_pe = pefile.PE(data=data, fast_load=False)
    host_imports = _verify_imports(_imports(host_pe), HOST_REQUIRED_IMPORTS, "host")

    host_offsets: dict[str, list[str]] = {}
    for marker in HOST_AUTH_MARKERS:
        hits = _find_all(data, marker.encode("ascii"))
        if not hits:
            raise InspectError(f"host marker {marker!r} not found")
        host_offsets[marker] = [f"0x{hit:X}" for hit in hits]

    cluster_required = (
        r"HKLM\SOFTWARE\Microsoft\Cryptography/vMachineGuid",
        "DEVICE_KEY_CLEANUP_PENDING",
        "packageKeyEnvelope",
        "packageKeyEnvelopeExpiresAt",
        "KEY_ENVELOPE_INVALID",
        "LWKE1",
        "device-key-cleanup.pending",
        "authorization.challenge",
        "authorization.ticket",
        "package-key.envelope",
        "build.manifest",
        "DEVICE_KEY_MISSING",
        "DEVICE_KEY_MISMATCH",
        "DEVICE_KEY_UNAVAILABLE",
        "KEY_ENVELOPE_EXPIRED",
    )
    auth_source_hits = [int(value, 16) for value in host_offsets[r"src\services\auth.rs"]]
    matching_clusters: list[tuple[int, int]] = []
    for candidate in auth_source_hits:
        candidate_end = candidate + 0x600
        if all(
            any(
                candidate <= int(value, 16) < candidate_end
                for value in host_offsets[marker]
            )
            for marker in cluster_required
        ):
            matching_clusters.append((candidate, candidate_end))
    if len(matching_clusters) != 1:
        raise InspectError(
            f"expected one auth-service marker cluster, got {matching_clusters}"
        )
    auth_cluster_begin, auth_cluster_end = matching_clusters[0]
    cluster_offsets: dict[str, list[str]] = {}
    for marker in cluster_required:
        candidates = [
            int(value, 16)
            for value in host_offsets[marker]
            if auth_cluster_begin <= int(value, 16) < auth_cluster_end
        ]
        if not candidates:
            raise InspectError(
                f"missing {marker!r} marker in auth cluster "
                f"0x{auth_cluster_begin:X}-0x{auth_cluster_end:X}"
            )
        cluster_offsets[marker] = [f"0x{candidate:X}" for candidate in candidates]

    proxies: list[dict[str, Any]] = []
    for name, spec in EMBEDDED_PROXIES.items():
        offset = int(spec["offset"])
        size = int(spec["size"])
        proxy = data[offset : offset + size]
        proxy_digest = _sha256(proxy)
        if proxy_digest != spec["sha256"]:
            raise InspectError(
                f"embedded {name} SHA-256 {proxy_digest}; expected {spec['sha256']}"
            )
        proxy_pe = pefile.PE(data=proxy, fast_load=False)
        proxy_imports = _verify_imports(
            _imports(proxy_pe), PROXY_REQUIRED_IMPORTS, name
        )
        ascii_offsets: dict[str, list[str]] = {}
        for marker in PROXY_ASCII_MARKERS:
            hits = _find_all(proxy, marker.encode("ascii"))
            if not hits:
                raise InspectError(f"{name} missing ASCII marker {marker!r}")
            ascii_offsets[marker] = [f"0x{hit:X}" for hit in hits]
        utf16_offsets = {
            marker: f"0x{_find_once(proxy, marker.encode('utf-16le'), marker):X}"
            for marker in PROXY_UTF16_MARKERS
        }
        proxies.append(
            {
                "name": name,
                "sha256": proxy_digest,
                "asciiMarkers": ascii_offsets,
                "utf16Markers": utf16_offsets,
                "requiredImports": proxy_imports,
            }
        )

    return {
        "findingId": "LWB-R6-041",
        "date": "2026-09-09",
        "scope": "PM7-B device-key/package-envelope prerequisite boundary",
        "evidenceLabel": "RECOVERED",
        "source": {
            "path": str(path),
            "sha256": digest,
            "hostCoordinateSystem": "raw file offsets for marker locations",
        },
        "tools": {
            "python": "3.12.10",
            "pefile": pefile.__version__,
        },
        "hostAuthCluster": {
            "sourceMarker": r"src\services\auth.rs",
            "window": f"0x{auth_cluster_begin:X}-0x{auth_cluster_end:X}",
            "markerOffsets": cluster_offsets,
            "requiredImports": host_imports,
        },
        "proxies": proxies,
        "result": [
            "The host auth-service marker cluster contains the Windows MachineGuid fingerprint path, device-key cleanup/error vocabulary, packageKeyEnvelope fields, LWKE1, and all four bridge-runtime material filenames.",
            "The verified host imports persistent Windows CNG-key primitives plus DPAPI protect/unprotect primitives; import presence does not by itself prove which primitive implements each auth-service branch.",
            "Both embedded proxies contain the same LWKE1/device-key/key-envelope/package-decrypt vocabulary and URL-safe Base64 alphabet together with the runtime-root/package-key/build-manifest/script-package paths.",
            "Both proxies import Windows CNG secret-agreement/derive-key functions, BCrypt key/decrypt/hash/signature functions, and file/environment functions needed by a runtime loader; the exact call graph, parameters, algorithms, and file-open/read function remain unrecovered.",
        ],
        "reproduction": [
            "python tools\\inspect_lwbridge_device_key_boundary.py ..\\LW\\lwbridge-0.3.1.exe",
            "python tools\\inspect_lwbridge_device_key_boundary.py ..\\LW\\lwbridge-0.3.1.exe --output evidence\\lwbridge-implementation\\2026-09-09-r6-device-key-boundary.json",
            "python -m py_compile tools\\inspect_lwbridge_device_key_boundary.py",
        ],
        "limits": {
            "staticOnly": True,
            "notProven": [
                "the proxy function that combines LWBRIDGE_PROFILE_RUNTIME_ROOT with package-key.envelope and opens/reads it",
                "the exact device-key storage name/provider and whether DPAPI or persistent CNG storage is authoritative for the proxy path",
                "the exact package-key envelope grammar, public-key/nonce/tag layout, key-agreement KDF parameters, and package cipher parameters",
                "bridge-scripts.dat plaintext or getWorldMapState/getCurrentServerId handler implementation",
                "propagated transport/readiness errors and the exact map scan state is unavailable trigger",
                "live correlation against a running current Last War/LWBridge target",
            ],
            "interpretationRule": "Imports and adjacent marker vocabulary establish available primitives and a shared auth/device-key boundary; they are not treated as proof of the exact runtime call graph or algorithm selection.",
        },
        "implementationImpact": {
            "publicBehaviorEnabled": False,
            "pm7B": "continue from the now-narrowed device-key/envelope loader seam to a permitted exact read/decrypt call trace or live authoritative correlation",
            "mapSummary": "remains fail-closed until the protected handler/readiness/error mapping is recovered",
        },
    }


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("binary", type=Path)
    parser.add_argument("--output", type=Path)
    args = parser.parse_args()
    try:
        result = inspect(args.binary)
    except (InspectError, OSError, pefile.PEFormatError) as exc:
        parser.error(str(exc))
    encoded = json.dumps(result, indent=2, ensure_ascii=False) + "\n"
    if args.output:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(encoded, encoding="utf-8")
    else:
        print(encoded, end="")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
