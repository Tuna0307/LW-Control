#!/usr/bin/env python3
"""Verify the proxy-side device-key configuration vocabulary.

This build-specific inspector is read-only. It hash-gates lwbridge-0.3.1.exe,
extracts both embedded xLua proxies, verifies the exact proxy-local
DEVICE_KEY_PROVIDER/EXPORT/FORMAT markers and proves those marker names are not
duplicated by host ASCII/UTF-16 literals outside the embedded proxy images.
It does not infer provider values, key names, algorithms, or loader call flow.
"""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
from typing import Any


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

EXPECTED_MARKERS = {
    "DEVICE_KEY_PROVIDER": 0x730E0,
    "DEVICE_KEY_EXPORT": 0x730F8,
    "DEVICE_KEY_FORMAT": 0x73110,
    "DEVICE_KEY_MISSING": 0x73128,
}

PROXY_ONLY_MARKERS = {
    "DEVICE_KEY_PROVIDER",
    "DEVICE_KEY_EXPORT",
    "DEVICE_KEY_FORMAT",
}


class InspectError(ValueError):
    pass


def _sha256(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def _find_all(data: bytes, marker: bytes) -> list[int]:
    hits: list[int] = []
    start = 0
    while True:
        hit = data.find(marker, start)
        if hit < 0:
            return hits
        hits.append(hit)
        start = hit + 1


def inspect(path: Path) -> dict[str, Any]:
    data = path.read_bytes()
    digest = _sha256(data)
    if digest != EXPECTED_SHA256:
        raise InspectError(
            f"unsupported lwbridge SHA-256 {digest}; expected {EXPECTED_SHA256}"
        )

    proxy_ranges: list[tuple[int, int]] = []
    proxies: list[dict[str, Any]] = []
    for name, spec in EMBEDDED_PROXIES.items():
        parent_offset = int(spec["offset"])
        size = int(spec["size"])
        proxy = data[parent_offset : parent_offset + size]
        proxy_digest = _sha256(proxy)
        if proxy_digest != spec["sha256"]:
            raise InspectError(
                f"embedded {name} SHA-256 {proxy_digest}; expected {spec['sha256']}"
            )
        proxy_ranges.append((parent_offset, parent_offset + size))

        marker_rows: dict[str, dict[str, str]] = {}
        for marker, expected_offset in EXPECTED_MARKERS.items():
            hits = _find_all(proxy, marker.encode("ascii"))
            if hits != [expected_offset]:
                raise InspectError(
                    f"{name} {marker} offsets {hits}; expected [0x{expected_offset:X}]"
                )
            marker_rows[marker] = {
                "proxyRawOffset": f"0x{expected_offset:X}",
                "hostRawOffset": f"0x{parent_offset + expected_offset:X}",
            }
        proxies.append(
            {
                "name": name,
                "sha256": proxy_digest,
                "markers": marker_rows,
            }
        )

    host_occurrences: dict[str, list[str]] = {}
    host_outside_proxy_occurrences: dict[str, list[str]] = {}
    utf16_occurrences: dict[str, list[str]] = {}
    for marker in EXPECTED_MARKERS:
        ascii_hits = _find_all(data, marker.encode("ascii"))
        host_occurrences[marker] = [f"0x{hit:X}" for hit in ascii_hits]
        outside = [
            hit
            for hit in ascii_hits
            if not any(begin <= hit < end for begin, end in proxy_ranges)
        ]
        if marker in PROXY_ONLY_MARKERS and outside:
            raise InspectError(
                f"unexpected host-side ASCII {marker} occurrences: "
                + ", ".join(f"0x{hit:X}" for hit in outside)
            )
        host_outside_proxy_occurrences[marker] = [f"0x{hit:X}" for hit in outside]

        wide_hits = _find_all(data, marker.encode("utf-16le"))
        if wide_hits:
            raise InspectError(
                f"unexpected UTF-16 {marker} occurrences: "
                + ", ".join(f"0x{hit:X}" for hit in wide_hits)
            )
        utf16_occurrences[marker] = []

    return {
        "findingId": "LWB-R6-042",
        "date": "2026-09-09",
        "scope": "PM7-B proxy device-key configuration vocabulary",
        "evidenceLabel": "RECOVERED",
        "source": {
            "path": str(path),
            "sha256": digest,
            "coordinateSystem": "raw file offsets in the host and embedded proxy images",
        },
        "proxies": proxies,
        "hostExactMarkerOccurrences": host_occurrences,
        "hostOutsideEmbeddedProxies": host_outside_proxy_occurrences,
        "hostUtf16Occurrences": utf16_occurrences,
        "result": [
            "Both verified proxies contain DEVICE_KEY_PROVIDER, DEVICE_KEY_EXPORT, DEVICE_KEY_FORMAT, and DEVICE_KEY_MISSING at the same proxy raw offsets 0x730E0/0x730F8/0x73110/0x73128.",
            "DEVICE_KEY_PROVIDER, DEVICE_KEY_EXPORT, and DEVICE_KEY_FORMAT occur in the host executable only as bytes inside the two embedded proxy images; DEVICE_KEY_MISSING also has host auth-service occurrences and is therefore a shared state/error label rather than proxy-only configuration vocabulary.",
            "These markers establish proxy-side configuration vocabulary only. They do not recover the provider value, persisted key/container name, export blob format value, key format value, or how the host/proxy supplies them at runtime.",
        ],
        "reproduction": [
            "python tools\\inspect_lwbridge_proxy_device_key_config.py ..\\LW\\lwbridge-0.3.1.exe",
            "python tools\\inspect_lwbridge_proxy_device_key_config.py ..\\LW\\lwbridge-0.3.1.exe --output evidence\\lwbridge-implementation\\2026-09-09-r6-proxy-device-key-config.json",
            "python -m py_compile tools\\inspect_lwbridge_proxy_device_key_config.py",
        ],
        "limits": {
            "staticOnly": True,
            "notProven": [
                "the runtime value of DEVICE_KEY_PROVIDER",
                "the persisted device-key/container name",
                "the runtime values of DEVICE_KEY_EXPORT and DEVICE_KEY_FORMAT",
                "the mechanism that supplies these values to the proxy",
                "the proxy package-key open/read/decrypt call graph and crypto parameters",
                "protected bridge handler plaintext and readiness/error mapping",
            ],
            "interpretationRule": "Exact marker ownership narrows the configuration boundary; marker names are not treated as their runtime values or as proof of a specific crypto primitive.",
        },
        "implementationImpact": {
            "publicBehaviorEnabled": False,
            "pm7B": "continue from exact proxy configuration names to a source-attributed value/provisioning path or supported live environment correlation",
            "mapSummary": "remains fail-closed",
        },
    }


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("binary", type=Path)
    parser.add_argument("--output", type=Path)
    args = parser.parse_args()
    try:
        result = inspect(args.binary)
    except (InspectError, OSError) as exc:
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
