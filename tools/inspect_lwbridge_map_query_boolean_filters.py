#!/usr/bin/env python3
"""Pin recovered LWBridge 0.3.1 Map Data boolean filter predicates."""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path


EXPECTED_SHA256 = "2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff"

MARKERS = {
    "specialOnlyField": "specialOnly",
    "specialOnlyPredicate": "CAST(json_extract(data_json,'$.isSpecial') AS INTEGER) = 1",
    "reindeerOnlyField": "reindeerOnly",
    "reindeerOnlyPredicate": "CAST(json_extract(data_json,'$.isSpecialURQuality') AS INTEGER) = 1",
}


def locate(blob: bytes, marker: str) -> int:
    encoded = marker.encode("ascii")
    count = blob.count(encoded)
    if count != 1:
        raise SystemExit(f"expected one recovered marker, found {count}: {marker}")
    offset = blob.find(encoded)
    if offset < 0:
        raise SystemExit(f"missing recovered marker: {marker}")
    return offset


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("binary", type=Path)
    parser.add_argument("--json", action="store_true")
    args = parser.parse_args()

    blob = args.binary.read_bytes()
    digest = hashlib.sha256(blob).hexdigest()
    if digest != EXPECTED_SHA256:
        raise SystemExit(f"unexpected LWBridge SHA-256: {digest}")

    offsets = {name: locate(blob, marker) for name, marker in MARKERS.items()}
    if offsets["specialOnlyPredicate"] != offsets["specialOnlyField"] + len(MARKERS["specialOnlyField"]):
        raise SystemExit("specialOnly field/predicate are not adjacent")
    if offsets["reindeerOnlyPredicate"] != offsets["reindeerOnlyField"] + len(MARKERS["reindeerOnlyField"]):
        raise SystemExit("reindeerOnly field/predicate are not adjacent")
    result = {
        "schemaVersion": 1,
        "date": "2026-09-08",
        "findingId": "LWB-R6-006",
        "scope": "Map Data special-only and reindeer-only backend predicates",
        "reference": {
            "path": str(args.binary),
            "sha256": digest,
            "coordinateSystem": "raw PE file byte offset",
        },
        "recovered": {
            "predicates": {
                "specialOnly": MARKERS["specialOnlyPredicate"],
                "reindeerOnly": MARKERS["reindeerOnlyPredicate"],
            },
            "fileOffsets": {name: f"0x{offset:08X}" for name, offset in offsets.items()},
            "frontendKinds": {
                "specialOnly": ["dispatch", "ghost"],
                "reindeerOnly": ["truck", "railway"],
            },
            "correlatedFrontendFinding": "LWB-R6-004",
        },
        "reproduction": (
            "python tools\\inspect_lwbridge_map_query_boolean_filters.py "
            "..\\LW\\lwbridge-0.3.1.exe --json"
        ),
        "implementationImpact": {
            "files": [
                "src/LWBridge.Desktop/MapDataQueryContract.cs",
                "src/LWBridge.Desktop/MapDataStore.cs",
                "tests/LWBridge.Desktop.Checks/Program.cs",
            ],
            "result": (
                "frontend-emitted true forms are accepted only for the recovered kind "
                "families and applied to count/page SQL"
            ),
        },
        "validation": {
            "status": "RECOVERED static plus IMPLEMENTED/OFFLINE-TESTED",
            "checks": [
                "verified reference SHA-256",
                "verified each field and predicate occurs once and is byte-adjacent",
                "deterministic LWBridge.Desktop.Checks mapContract suite",
            ],
            "limits": (
                "no live scan or native ingestion; adjacent quality, time, plunder, "
                "treasure and alternate-sort semantics remain fail-closed"
            ),
        },
    }

    if args.json:
        print(json.dumps(result, indent=2, ensure_ascii=False))
    else:
        for name, offset in offsets.items():
            print(f"{name}: 0x{offset:08X} {MARKERS[name]}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
