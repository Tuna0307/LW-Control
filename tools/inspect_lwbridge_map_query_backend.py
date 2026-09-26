#!/usr/bin/env python3
"""Pin recovered LWBridge 0.3.1 Map Data backend predicate strings."""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path


EXPECTED_SHA256 = "2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff"

MARKERS = {
    "alliance": "alliance_name = ?",
    "withoutAlliance": "(alliance_name IS NULL OR alliance_name = '')",
    "resourceNameKey": "CAST(json_extract(data_json,'$.resourceNameKey') AS TEXT) = ?",
    "monsterNameKey": "CAST(json_extract(data_json,'$.monsterNameKey') AS TEXT) = ?",
    "itemKeyPrefix": "itemKey EXISTS (SELECT 1 FROM json_each(",
    "itemKeyValue": "CAST(json_extract(good.value,'$.key') AS TEXT) = ?",
}


def locate(blob: bytes, marker: str) -> int:
    encoded = marker.encode("ascii")
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
    result = {
        "schemaVersion": 1,
        "date": "2026-09-08",
        "findingId": "LWB-R6-005",
        "scope": "Map Data backend predicate strings",
        "reference": {
            "path": str(args.binary),
            "sha256": digest,
            "coordinateSystem": "raw PE file byte offset",
        },
        "recovered": {
            "predicates": {
                "alliance": MARKERS["alliance"],
                "withoutAlliance": MARKERS["withoutAlliance"],
                "resourceNameKey": MARKERS["resourceNameKey"],
                "monsterNameKey": MARKERS["monsterNameKey"],
                "itemKey": {
                    "existsPrefix": MARKERS["itemKeyPrefix"],
                    "valuePredicate": MARKERS["itemKeyValue"],
                    "jsonArray": "$.currentGoods",
                },
            },
            "fileOffsets": {name: f"0x{offset:08X}" for name, offset in offsets.items()},
        },
        "implementationBoundary": {
            "implementedOffline": [
                "city alliance equality",
                "city no-alliance predicate when the frontend emits withoutAlliance=true",
                "resource resourceNameKey equality",
                "monster monsterNameKey equality",
                "truck/railway currentGoods itemKey membership",
            ],
            "stillBlocked": [
                "keyword escaping and LIKE parameter construction",
                "quality/special/reindeer semantics",
                "completionStatus current-time source and units",
                "plunderableOnly per-kind predicate selection",
                "treasure visibility/lucky ordering semantics",
                "alternate sort expression mapping",
                "map_data_options aggregation and map_summary server selection",
            ],
        },
        "reproduction": (
            "python tools\\inspect_lwbridge_map_query_backend.py "
            "..\\LW\\lwbridge-0.3.1.exe --json"
        ),
        "implementationImpact": {
            "files": [
                "src/LWBridge.Desktop/MapDataQueryContract.cs",
                "src/LWBridge.Desktop/MapDataStore.cs",
                "tests/LWBridge.Desktop.Checks/Program.cs",
            ],
            "result": "the recovered predicate subset is accepted by map_search and applied to persisted SQLite rows",
        },
        "validation": {
            "status": "RECOVERED static plus IMPLEMENTED/OFFLINE-TESTED",
            "checks": [
                "verified reference SHA-256 and marker offsets",
                "deterministic LWBridge.Desktop.Checks mapContract suite",
            ],
            "limits": "no Last War process or LWBridge live feature execution; blocked semantics remain fail-closed",
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
