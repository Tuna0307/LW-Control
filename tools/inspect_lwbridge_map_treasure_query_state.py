#!/usr/bin/env python3
"""Pin LWBridge 0.3.1 Treasure query/state-cache contracts."""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path

EXPECTED_SHA256 = "2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff"
LF = bytes((10,))

MARKERS: dict[str, bytes] = {
    "includeForeignRadarTreasures": b"includeForeignRadarTreasures",
    "foreignRadarExplicitViewerAlliance": (
        b"(COALESCE(CAST(json_extract(data_json,'$.treasureType') AS INTEGER),0)<>1"
        + LF
        + b"                      OR CAST(json_extract(data_json,'$.allianceId') AS TEXT)=?)"
    ),
    "foreignRadarEmbeddedViewerAlliance": (
        b"(COALESCE(CAST(json_extract(data_json,'$.treasureType') AS INTEGER),0)<>1"
        + LF
        + b"                      OR (COALESCE(CAST(json_extract(data_json,'$.allianceId') AS TEXT),'')<>''"
        + LF
        + b"                        AND CAST(json_extract(data_json,'$.allianceId') AS TEXT)="
        + b"COALESCE(CAST(json_extract(data_json,'$.viewerAllianceId') AS TEXT),'')))"
    ),
    "luckyFirst": b"luckyFirst",
    "luckyPriority": (
        b"COALESCE((SELECT CAST(json_extract(state.state_json,'$.claimPriority') AS INTEGER)"
        + LF
        + b"                 FROM treasure_claim_states AS state"
    ),
    "stateJoin": (
        b"LEFT JOIN treasure_claim_states AS state ON state.server_id=page.server_id AND state.player_uid="
    ),
    "stateCleanup": (
        b"DELETE FROM treasure_claim_states WHERE expire_time IS NOT NULL AND expire_time>0 AND expire_time<=?1"
    ),
    "stateUpsert": b"INSERT INTO treasure_claim_states(",
    "inspectTreasureStates": b"inspectTreasureStates",
    "getTreasureClaimStatus": b"getTreasureClaimStatus",
}


def locate_once(blob: bytes, marker: bytes) -> int:
    count = blob.count(marker)
    if count != 1:
        raise SystemExit(
            f"expected one marker, found {count}: {marker[:100]!r}"
        )
    return blob.find(marker)


def marker_text(value: bytes) -> str:
    return value.decode("ascii")


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("binary", type=Path)
    parser.add_argument("--json", action="store_true")
    args = parser.parse_args()

    blob = args.binary.read_bytes()
    digest = hashlib.sha256(blob).hexdigest()
    if digest != EXPECTED_SHA256:
        raise SystemExit(f"unexpected LWBridge SHA-256: {digest}")

    offsets = {name: locate_once(blob, marker) for name, marker in MARKERS.items()}
    expected_offsets = {
        "includeForeignRadarTreasures": 0x00C89511,
        "foreignRadarExplicitViewerAlliance": 0x00C8952D,
        "foreignRadarEmbeddedViewerAlliance": 0x00C895C7,
        "luckyFirst": 0x00C8A3AF,
        "luckyPriority": 0x00C8A424,
        "stateJoin": 0x00C8A743,
        "stateCleanup": 0x00C883FA,
        "stateUpsert": 0x00C88472,
        "inspectTreasureStates": 0x008226B0,
        "getTreasureClaimStatus": 0x00822630,
    }
    for name, expected in expected_offsets.items():
        if offsets[name] != expected:
            raise SystemExit(
                f"unexpected offset for {name}: {offsets[name]:#x} != {expected:#x}"
            )

    result = {
        "schemaVersion": 1,
        "findingId": "LWB-R7-068",
        "date": "2026-09-20",
        "scope": (
            "Treasure visibility, viewer identity, lucky ordering and "
            "persistent claim-state cache"
        ),
        "reference": {
            "path": str(args.binary),
            "sha256": digest,
            "coordinateSystem": "raw PE file byte offset",
        },
        "recovered": {
            "foreignRadar": {
                "falseExplicitViewerAlliancePredicate": marker_text(
                    MARKERS["foreignRadarExplicitViewerAlliance"]
                ),
                "falseEmbeddedViewerAlliancePredicate": marker_text(
                    MARKERS["foreignRadarEmbeddedViewerAlliance"]
                ),
                "trueBehavior": "predicate omitted",
            },
            "luckyFirst": {
                "priorityExpressionPrefix": marker_text(MARKERS["luckyPriority"]),
                "missingStatePriority": 1,
                "direction": "ASC",
                "viewerUidFallback": (
                    "COALESCE(CAST(json_extract(page.data_json,'$.viewerUid') AS TEXT),'')"
                ),
            },
            "claimStateCache": {
                "cleanup": marker_text(MARKERS["stateCleanup"]),
                "upsertPrefix": marker_text(MARKERS["stateUpsert"]),
                "pageJoinPrefix": marker_text(MARKERS["stateJoin"]),
                "key": ["server_id", "player_uid", "treasure_uuid"],
            },
            "bridgeReadOnlyRpcNames": [
                "inspectTreasureStates",
                "getTreasureClaimStatus",
            ],
            "fileOffsets": {
                name: f"0x{offset:08X}" for name, offset in offsets.items()
            },
        },
        "validation": {
            "status": (
                "RECOVERED static; query/cache slice IMPLEMENTED/OFFLINE-TESTED"
            ),
            "limits": (
                "The hidden bridge script's complete world/player state-label "
                "mapping is not exposed by these Rust strings. State-changing "
                "treasure claim remains blocked."
            ),
        },
        "reproduction": (
            "python tools\\inspect_lwbridge_map_treasure_query_state.py "
            "..\\LW\\lwbridge-0.3.1.exe --json"
        ),
    }

    if args.json:
        print(json.dumps(result, indent=2))
    else:
        for name, offset in offsets.items():
            print(f"{name}: 0x{offset:08X}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
